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
        string prompt = $@"You are a web-grounded comparative fiqh research agent for TijarahAI.
Research the user's commercial or Islamic-finance question: ""{query}"".

Use Google Search grounding to prioritize authoritative primary or institutional sources, such as AAOIFI, recognized Dar al-Ifta bodies, IslamQA, Darul Iftaa, and established scholarly publications. Do not invent sources or claim a consensus where credible sources disagree. This is educational research, not a binding fatwa.

Return concise plain text using these exact headings:
HANAFI PERSPECTIVE:
[Summarize the Hanafi position, conditions, and any uncertainty.]

AHL AL-HADITH:
[Summarize the Ahl al-Hadith position, conditions, and any uncertainty.]

CONSENSUS & DIFFERENCES:
[Compare the two perspectives, give a practical next step, and flag material disagreement.]

Base factual claims on grounded sources. The application will display the source links separately.";

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
