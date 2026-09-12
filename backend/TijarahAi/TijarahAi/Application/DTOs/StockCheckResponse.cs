namespace TijarahAi.Application.DTOs;

public class StockCheckResponse
{
    public string Ticker { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal MarketCap { get; set; }
    public StockComplianceAssessment StrictStandard { get; set; } = new();
    public StockComplianceAssessment AaoifiStandard { get; set; } = new();
    public DateTime Timestamp { get; set; }
}
