using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TijarahAi.Application.Common.Interfaces;
using TijarahAi.Application.DTOs;
using TijarahAi.Domain.Entities;
using TijarahAi.Domain.Enums;

namespace TijarahAi.Application.Services;

public class FiqhAdvisorService
{
    private readonly IVectorStore _vectorStore;
    private readonly IGeminiClient _geminiClient;
    private readonly ILogger<FiqhAdvisorService> _logger;

    public FiqhAdvisorService(
        IVectorStore vectorStore,
        IGeminiClient geminiClient,
        ILogger<FiqhAdvisorService> logger)
    {
        _vectorStore = vectorStore;
        _geminiClient = geminiClient;
        _logger = logger;
    }

    public async Task<FiqhQueryResponse> AnswerComparativeFiqhQueryAsync(string question, CancellationToken cancellationToken = default)
    {
        // 1. Generate query embedding using Gemini
        var queryEmbedding = await _geminiClient.GenerateEmbeddingAsync(question, cancellationToken);

        // 2. Strict RAG separation: retrieve Hanafi and Ahl-e-Hadith independently in parallel
        var hanafiChunksTask = _vectorStore.SearchAsync(Madhab.Hanafi, queryEmbedding, topK: 3, cancellationToken);
        var ahleHadithChunksTask = _vectorStore.SearchAsync(Madhab.AhleHadith, queryEmbedding, topK: 3, cancellationToken);

        await Task.WhenAll(hanafiChunksTask, ahleHadithChunksTask);

        var hanafiChunks = await hanafiChunksTask;
        var ahleHadithChunks = await ahleHadithChunksTask;

        hanafiChunks = FilterEvidenceForQuestion(question, hanafiChunks);
        ahleHadithChunks = FilterEvidenceForQuestion(question, ahleHadithChunks);

        // 3. Synthesize Hanafi Answer using LLM with strictly grounded evidence
        var hanafiPerspective = await SynthesizePerspectiveAsync("Hanafi", question, hanafiChunks, cancellationToken);

        // 4. Synthesize Ahl-e-Hadith Answer using LLM with strictly grounded evidence
        var ahleHadithPerspective = await SynthesizePerspectiveAsync("Ahl-e-Hadith", question, ahleHadithChunks, cancellationToken);

        // 5. Generate a question-specific comparative summary
        var comparativeSummary = await GenerateComparativeSummaryAsync(
            question,
            hanafiPerspective,
            ahleHadithPerspective,
            cancellationToken);

        return new FiqhQueryResponse
        {
            Question = question,
            HanafiPerspective = hanafiPerspective,
            AhleHadithPerspective = ahleHadithPerspective,
            ComparativeSummary = comparativeSummary,
            Timestamp = DateTime.UtcNow
        };
    }

    private static List<KnowledgeChunk> FilterEvidenceForQuestion(
        string question,
        List<KnowledgeChunk> evidence)
    {
        if (!IsStockQuestion(question))
            return evidence;

        string[] stockEvidenceTerms =
        [
            "stock",
            "shares",
            "equity",
            "securities",
            "market capitalisation",
            "market capitalization",
            "financial paper",
            "sukuk"
        ];

        return evidence
            .Where(chunk => stockEvidenceTerms.Any(term =>
                chunk.Content.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                chunk.Topic.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                chunk.StandardOrBook.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static bool IsStockQuestion(string question)
    {
        string[] stockQuestionTerms = ["stock", "stocks", "share", "shares", "equity", "securities", "sukuk", "bond", "bonds"];
        return stockQuestionTerms.Any(term => question.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<FiqhPerspectiveResult> SynthesizePerspectiveAsync(
        string madhabName,
        string question,
        List<KnowledgeChunk> evidence,
        CancellationToken cancellationToken)
    {
        if (evidence.Count == 0)
        {
            return new FiqhPerspectiveResult
            {
                Perspective = madhabName,
                Ruling = "No relevant evidence found",
                Reasoning = $"The knowledge base does not contain relevant evidence for this question from the {madhabName} perspective."
            };
        }

        var evidenceBuilder = new StringBuilder();
        var citations = new List<string>();

        foreach (var chunk in evidence)
        {
            evidenceBuilder.AppendLine($"--- [SOURCE: {chunk.StandardOrBook}] ---");
            evidenceBuilder.AppendLine($"[TOPIC]: {chunk.Topic} - {chunk.SectionTitle}");
            evidenceBuilder.AppendLine($"[CONTENT]: {chunk.Content}");
            evidenceBuilder.AppendLine($"[CITATIONS]: {chunk.Citations}");
            evidenceBuilder.AppendLine();

            if (!string.IsNullOrWhiteSpace(chunk.Citations))
                citations.Add($"{chunk.StandardOrBook}: {chunk.Citations}");
            else if (!string.IsNullOrWhiteSpace(chunk.StandardOrBook))
                citations.Add(chunk.StandardOrBook);
        }

        string systemPrompt = $@"You are the {madhabName} Jurisprudence Specialist for TijarahAI, an authoritative Islamic business intelligence platform.
            Answer the user's commercial inquiry strictly from the {madhabName} perspective using ONLY the provided authoritative evidence excerpts.
CRITICAL RULES:
1. Do NOT mix opinions from any other school of thought.
2. If the provided evidence does not address a specific point, explicitly state: 'This specific nuance is not covered in the retrieved texts; consult a qualified {madhabName} scholar.'
            3. The ruling must be a concise answer to the user's question, such as 'Permissible with conditions', 'Not permissible', or 'Insufficient evidence'. Never use 'Retrieved from Knowledge Base' or any similar retrieval-status phrase as a ruling.
            4. Output valid JSON adhering to the following schema:
{{
  ""ruling"": ""Clear statement (e.g., Permissible / Prohibited / Permissible with Conditions)"",
  ""reasoning"": ""Detailed explanation derived directly from the sources"",
  ""permissibleConditions"": [""Condition 1"", ""Condition 2""],
  ""prohibitions"": [""Prohibition 1"", ""Prohibition 2""],
  ""citations"": [""Source 1 with exact clause/page""]
}}
Return ONLY the raw JSON object.";

        string userPrompt = $"User Query: \"{question}\"\n\nAuthoritative Retrieved Evidence:\n{evidenceBuilder}";

        try
        {
            string rawResponse = await _geminiClient.GenerateTextAsync(systemPrompt, userPrompt, cancellationToken);
            string cleanJson = CleanJson(rawResponse);
            var parsed = JsonSerializer.Deserialize<FiqhPerspectiveResult>(cleanJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (parsed != null)
            {
                parsed.Perspective = madhabName;
                if (parsed.Citations == null || parsed.Citations.Count == 0)
                    parsed.Citations = citations.Distinct().ToList();
                return parsed;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate the {Madhab} ruling for question {Question}.", madhabName, question);
            return new FiqhPerspectiveResult
            {
                Perspective = madhabName,
                Ruling = "Unable to generate a ruling",
                Reasoning = "The retrieved evidence was available, but the language model did not return a valid ruling. Please try again.",
                Citations = citations.Distinct().ToList()
            };
        }

        return new FiqhPerspectiveResult { Perspective = madhabName, Ruling = "Indeterminate", Reasoning = "Evidence processing error." };
    }

    private static string CleanJson(string raw)
    {
        string text = raw.Trim();
        if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            text = text[7..];
        else if (text.StartsWith("```"))
            text = text[3..];

        if (text.EndsWith("```"))
            text = text[..^3];

        return text.Trim();
    }

    private async Task<string> GenerateComparativeSummaryAsync(
        string question,
        FiqhPerspectiveResult hanafi,
        FiqhPerspectiveResult ahleHadith,
        CancellationToken cancellationToken)
    {
        if (hanafi.Ruling == "No relevant evidence found" && ahleHadith.Ruling == "No relevant evidence found")
            return "No relevant fiqh evidence was found in the knowledge base for this question.";

        string systemPrompt = """
            You are a comparative fiqh editor. Create a clear and concise comparison
            of the two provided perspectives for the user's exact question.
            Use only the supplied perspectives and do not add rulings, sources, or
            assumptions that are not present in them. Mention when one perspective
            has no relevant evidence. Return only one plain-text paragraph of no
            more than 80 words. Do not use JSON, headings, or markdown.
            """;

        string userPrompt = $"""
            Question: {question}

            Hanafi perspective:
            {JsonSerializer.Serialize(hanafi)}

            Ahl-e-Hadith perspective:
            {JsonSerializer.Serialize(ahleHadith)}
            """;

        try
        {
            string summary = await _geminiClient.GenerateTextAsync(systemPrompt, userPrompt, cancellationToken);
            summary = CleanJson(summary);
            return string.IsNullOrWhiteSpace(summary)
                ? BuildComparativeSummaryFallback(question, hanafi, ahleHadith)
                : summary;
        }
        catch
        {
            return BuildComparativeSummaryFallback(question, hanafi, ahleHadith);
        }
    }

    private static string BuildComparativeSummaryFallback(
        string question,
        FiqhPerspectiveResult hanafi,
        FiqhPerspectiveResult ahleHadith)
    {
        if (hanafi.Ruling == "No relevant evidence found" && ahleHadith.Ruling == "No relevant evidence found")
            return "No relevant fiqh evidence was found in the knowledge base for this question.";

        if (ahleHadith.Ruling == "No relevant evidence found")
            return $"For '{question}', the Hanafi evidence indicates: {hanafi.Ruling}. No relevant Ahl-e-Hadith evidence was found in the knowledge base.";

        if (hanafi.Ruling == "No relevant evidence found")
            return $"For '{question}', no relevant Hanafi evidence was found in the knowledge base. The Ahl-e-Hadith evidence indicates: {ahleHadith.Ruling}.";

        return $"For '{question}', the Hanafi perspective concludes '{hanafi.Ruling}', while the Ahl-e-Hadith perspective concludes '{ahleHadith.Ruling}'. Review the cited evidence for the specific conditions and differences.";
    }
}
