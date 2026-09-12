using TijarahAi.Domain.Enums;

namespace TijarahAi.Domain.Entities;

public class ComplianceRuleEvaluation
{
    public string RuleName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal CalculatedValue { get; set; }
    public decimal AllowedThreshold { get; set; }
    public bool Passed { get; set; }
    public string Explanation { get; set; } = string.Empty;
}

public class ComplianceResult
{
    public string Ticker { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public ComplianceStandard StandardApplied { get; set; }
    public ComplianceStatus Status { get; set; }
    public List<ComplianceRuleEvaluation> RuleEvaluations { get; set; } = new();
    public decimal PurificationPercentage { get; set; }
    public string SummaryText { get; set; } = string.Empty;
    public string AiExecutiveAudit { get; set; } = string.Empty;
    public DateTime EvaluatedAtUtc { get; set; } = DateTime.UtcNow;
}
