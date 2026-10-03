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
        string prompt = $@"You are the Web Research Fatwa Agent for TijarahAI.
The user is asking about a modern commercial transaction: ""{query}"".

Search authoritative Islamic research portals, such as:
- Darul Iftaa / Deoband (Hanafi)
- IslamQA (Sheikh Al-Munajjid / Saudi Senior Scholars) (always preffered)
- AAOIFI Official Publications
- IslamWeb

Structure your response clearly with these 3 sections:
1. [HANAFI PERSPECTIVE]: Explain the ruling, underlying legal maxims (Istihsan, Urf, Qabd Hukmi), and conditions.
2. [AHL AL-HADITH / SAUDI SCHOLARS PERSPECTIVE]: Explain the direct scriptural evidence, Hadith rulings, and conditions.
3. [CONSENSUS & DIFFERENCES]: A concise 2-sentence summary comparing both positions for the user.

Ensure you cite the scholars or institutions referenced.";

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
