using TijarahAi.Application.DTOs;
using TijarahAi.Infrastructure.AI;

namespace TijarahAi.Application.Services;

public class WebResearchAgentService
{
    private readonly IGeminiSearchClient _searchClient;

    public WebResearchAgentService(IGeminiSearchClient searchClient)
    {
        _searchClient = searchClient;
    }

    public async Task<WebResearchResponse> ConductDeepWebResearchAsync(string query, CancellationToken cancellationToken = default)
    {
        string prompt = $@"You are a web research agent for TijarahAI.
Research the user's query: ""{query}"".

Use Google Search grounding to find current, authoritative, relevant sources. Do not assume the query is about Islam, contracts, finance, or law. Do not introduce a religious or legal perspective unless the query explicitly asks for one.

Return a concise answer with:
1. A direct summary of the findings.
2. Practical recommendations or key considerations.
3. Important limitations, risks, or disagreements between sources.

Base factual claims on the grounded sources and do not invent citations.";

        var (responseText, sources) = await _searchClient.GenerateWithWebSearchAsync(prompt, cancellationToken);

        string hanafi = ExtractSection(responseText, "HANAFI PERSPECTIVE", "AHL AL-HADITH");
        string ahleHadith = ExtractSection(responseText, "AHL AL-HADITH", "CONSENSUS & DIFFERENCES");
        string summary = ExtractSection(responseText, "CONSENSUS & DIFFERENCES", "");

        return new WebResearchResponse
        {
            Query = query,
            HanafiVerdict = string.IsNullOrWhiteSpace(hanafi) ? responseText : hanafi,
            AhleHadithVerdict = string.IsNullOrWhiteSpace(ahleHadith) ? "See detailed analysis." : ahleHadith,
            KeyConsensusAndDifferences = string.IsNullOrWhiteSpace(summary) ? "Both schools emphasize avoiding Gharar and Riba." : summary,
            WebSources = sources,
            Timestamp = DateTime.UtcNow
        };
    }

    private static string ExtractSection(string text, string startTag, string endTag)
    {
        int start = text.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
        if (start == -1) return string.Empty;
        start += startTag.Length;
        while (start < text.Length && (text[start] == ':' || text[start] == ']' || text[start] == '\n' || text[start] == '\r')) start++;

        if (string.IsNullOrEmpty(endTag)) return text[start..].Trim();

        int end = text.IndexOf(endTag, start, StringComparison.OrdinalIgnoreCase);
        if (end == -1) return text[start..].Trim();

        return text[start..end].Trim(' ', '\n', '\r', '-', '[');
    }
}
