using TijarahAi.Domain.Entities;
using TijarahAi.Domain.Enums;

namespace TijarahAi.Application.DTOs;

public class StockComplianceAssessment
{
    public ComplianceStandard StandardApplied { get; set; }
    public ComplianceStatus Status { get; set; }
    public List<ComplianceRuleEvaluation> Rules { get; set; } = new();
    public decimal DividendPurificationPercentage { get; set; }
    public string AiExecutiveAudit { get; set; } = string.Empty;
}
