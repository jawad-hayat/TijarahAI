namespace TijarahAi.Domain.Entities;

public class StockFinancials
{
    public string Ticker { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public string Sector { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal MarketCapitalization { get; set; }
    public decimal TotalDebt { get; set; }
    public decimal CashAndShortTermInvestments { get; set; }
    public decimal AccountsReceivable { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal InterestIncome { get; set; }
    public decimal NonCompliantRevenue { get; set; }
    public decimal AnnualDividendPerShare { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}
