namespace TijarahAi.Application.DTOs;

public class ContractAuditRequest
{
    public string ContractText { get; set; } = string.Empty;
    public string ContractType { get; set; } = "General Commercial";
}

public class RedlinedClause
{
    public int ClauseNumber { get; set; }
    public string OriginalClause { get; set; } = string.Empty;
    public string ShariahIssue { get; set; } = string.Empty;
    public string ProhibitedElement { get; set; } = string.Empty;
    public string RemediedClause { get; set; } = string.Empty;
    public string ScholarlyJustification { get; set; } = string.Empty;
    public string StandardCited { get; set; } = string.Empty;
}

public class ContractAuditResponse
{
    public string ContractType { get; set; } = string.Empty;
    public bool HasViolations { get; set; }
    public int TotalViolationsFound { get; set; }
    public List<RedlinedClause> RedlinedClauses { get; set; } = new();
    public string FullyRemediedContract { get; set; } = string.Empty;
    public string ExecutiveSummary { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class WebResearchRequest
{
    public string Query { get; set; } = string.Empty;
}

public class ResearchSource
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
}

public class WebResearchResponse
{
    public string Query { get; set; } = string.Empty;
    public string HanafiVerdict { get; set; } = string.Empty;
    public string AhleHadithVerdict { get; set; } = string.Empty;
    public string KeyConsensusAndDifferences { get; set; } = string.Empty;
    public List<ResearchSource> WebSources { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
