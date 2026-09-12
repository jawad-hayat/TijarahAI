using Microsoft.Extensions.Configuration;
using TijarahAi.Application.Common.Interfaces;
using TijarahAi.Domain.Entities;
using TijarahAi.Domain.Enums;

namespace TijarahAi.Application.Services;

public class StockComplianceEngine : IStockComplianceEngine
{
    private readonly decimal _aaoifiDebtRatioLimit;
    private readonly decimal _aaoifiCashRatioLimit;
    private readonly decimal _aaoifiReceivablesRatioLimit;
    private readonly decimal _aaoifiRevenueHaramLimit;

    public StockComplianceEngine(IConfiguration configuration)
    {
        // Configurable thresholds defaulting to AAOIFI Standard No. 21
        _aaoifiDebtRatioLimit = configuration.GetValue("ComplianceSettings:AaoifiDebtRatioLimit", 0.33m);
        _aaoifiCashRatioLimit = configuration.GetValue("ComplianceSettings:AaoifiCashRatioLimit", 0.33m);
        _aaoifiReceivablesRatioLimit = configuration.GetValue("ComplianceSettings:AaoifiReceivablesRatioLimit", 0.33m);
        _aaoifiRevenueHaramLimit = configuration.GetValue("ComplianceSettings:AaoifiRevenueHaramLimit", 0.05m);
    }

    public ComplianceResult Evaluate(StockFinancials financials, ComplianceStandard standard)
    {
        var result = new ComplianceResult
        {
            Ticker = financials.Ticker,
            CompanyName = financials.CompanyName,
            StandardApplied = standard,
            EvaluatedAtUtc = DateTime.UtcNow
        };

        if (financials.MarketCapitalization <= 0)
        {
            result.Status = ComplianceStatus.Questionable;
            result.SummaryText = "Insufficient market capitalization data to compute financial ratios.";
            return result;
        }

        if (standard == ComplianceStandard.Strict)
        {
            EvaluateStrictStandard(financials, result);
        }
        else
        {
            EvaluateAaoifiStandard(financials, result);
        }

        return result;
    }

    private void EvaluateStrictStandard(StockFinancials f, ComplianceResult result)
    {
        var debtEvaluation = new ComplianceRuleEvaluation
        {
            RuleName = "Zero Interest-Bearing Debt",
            Description = "Total debt must be exactly $0. Absolute zero tolerance for riba contracts.",
            CalculatedValue = f.TotalDebt,
            AllowedThreshold = 0.0m,
            Passed = f.TotalDebt <= 0,
            Explanation = f.TotalDebt <= 0 
                ? "Pass: Company carries $0 in interest-bearing debt." 
                : $"Fail: Company carries {f.TotalDebt:C0} in debt, which violates strict 0% Riba tolerance."
        };

        var interestIncomeEvaluation = new ComplianceRuleEvaluation
        {
            RuleName = "Zero Interest Income",
            Description = "Interest income must be exactly $0.",
            CalculatedValue = f.InterestIncome,
            AllowedThreshold = 0.0m,
            Passed = f.InterestIncome <= 0,
            Explanation = f.InterestIncome <= 0 
                ? "Pass: Zero interest income reported." 
                : $"Fail: Reported {f.InterestIncome:C0} in interest income."
        };

        result.RuleEvaluations.Add(debtEvaluation);
        result.RuleEvaluations.Add(interestIncomeEvaluation);

        bool allPassed = debtEvaluation.Passed && interestIncomeEvaluation.Passed;
        result.Status = allPassed ? ComplianceStatus.Compliant : ComplianceStatus.NonCompliant;
        result.PurificationPercentage = 0; // Strict standard rejects non-compliant stocks outright; no purification permitted
        result.SummaryText = allPassed 
            ? "Fully Compliant under Strict 0% Riba Standard." 
            : "Non-Compliant under Strict 0% Riba Standard due to presence of debt or interest income.";
    }

    private void EvaluateAaoifiStandard(StockFinancials f, ComplianceResult result)
    {
        decimal debtRatio = f.TotalDebt / f.MarketCapitalization;
        decimal cashRatio = f.CashAndShortTermInvestments / f.MarketCapitalization;
        decimal receivablesRatio = f.AccountsReceivable / f.MarketCapitalization;
        decimal haramRevenueRatio = f.TotalRevenue > 0 ? (f.NonCompliantRevenue / f.TotalRevenue) : 0m;

        var ruleDebt = new ComplianceRuleEvaluation
        {
            RuleName = "Debt to Market Capitalization",
            Description = $"Total interest-bearing debt must not exceed {_aaoifiDebtRatioLimit * 100:0}% of Market Cap.",
            CalculatedValue = Math.Round(debtRatio * 100, 2),
            AllowedThreshold = _aaoifiDebtRatioLimit * 100,
            Passed = debtRatio <= _aaoifiDebtRatioLimit,
            Explanation = debtRatio <= _aaoifiDebtRatioLimit
                ? $"Pass: Debt ratio ({debtRatio * 100:F2}%) is within the {_aaoifiDebtRatioLimit * 100:0}% threshold."
                : $"Fail: Debt ratio ({debtRatio * 100:F2}%) exceeds the {_aaoifiDebtRatioLimit * 100:0}% limit."
        };

        var ruleCash = new ComplianceRuleEvaluation
        {
            RuleName = "Cash & Securities to Market Cap",
            Description = $"Cash + Interest Securities must not exceed {_aaoifiCashRatioLimit * 100:0}% of Market Cap.",
            CalculatedValue = Math.Round(cashRatio * 100, 2),
            AllowedThreshold = _aaoifiCashRatioLimit * 100,
            Passed = cashRatio <= _aaoifiCashRatioLimit,
            Explanation = cashRatio <= _aaoifiCashRatioLimit
                ? $"Pass: Cash ratio ({cashRatio * 100:F2}%) is below {_aaoifiCashRatioLimit * 100:0}%."
                : $"Fail: Liquid cash ratio ({cashRatio * 100:F2}%) exceeds {_aaoifiCashRatioLimit * 100:0}%."
        };

        var ruleReceivables = new ComplianceRuleEvaluation
        {
            RuleName = "Accounts Receivable to Market Cap",
            Description = $"Accounts receivable must not exceed {_aaoifiReceivablesRatioLimit * 100:0}% of Market Cap.",
            CalculatedValue = Math.Round(receivablesRatio * 100, 2),
            AllowedThreshold = _aaoifiReceivablesRatioLimit * 100,
            Passed = receivablesRatio <= _aaoifiReceivablesRatioLimit,
            Explanation = receivablesRatio <= _aaoifiReceivablesRatioLimit
                ? $"Pass: Receivables ratio ({receivablesRatio * 100:F2}%) complies."
                : $"Fail: Receivables ratio ({receivablesRatio * 100:F2}%) exceeds {_aaoifiReceivablesRatioLimit * 100:0}%."
        };

        var ruleRevenue = new ComplianceRuleEvaluation
        {
            RuleName = "Non-Permissible Revenue Threshold",
            Description = $"Impure / non-compliant revenue must not exceed {_aaoifiRevenueHaramLimit * 100:0}% of Total Revenue.",
            CalculatedValue = Math.Round(haramRevenueRatio * 100, 2),
            AllowedThreshold = _aaoifiRevenueHaramLimit * 100,
            Passed = haramRevenueRatio <= _aaoifiRevenueHaramLimit,
            Explanation = haramRevenueRatio <= _aaoifiRevenueHaramLimit
                ? $"Pass: Non-permissible revenue ({haramRevenueRatio * 100:F2}%) is within acceptable 5% tolerance."
                : $"Fail: Non-permissible revenue ({haramRevenueRatio * 100:F2}%) exceeds 5% maximum ceiling."
        };

        result.RuleEvaluations.AddRange(new[] { ruleDebt, ruleCash, ruleReceivables, ruleRevenue });

        bool isCompliant = ruleDebt.Passed && ruleCash.Passed && ruleReceivables.Passed && ruleRevenue.Passed;
        result.Status = isCompliant ? ComplianceStatus.Compliant : ComplianceStatus.NonCompliant;

        // Dividend purification formula = Non-compliant revenue / Total revenue
        result.PurificationPercentage = Math.Round(haramRevenueRatio * 100, 2);
        result.SummaryText = isCompliant
            ? $"Compliant under AAOIFI Standard No. 21. Purification required: {result.PurificationPercentage}% of dividend income."
            : "Non-Compliant under AAOIFI Standard No. 21 due to exceeding financial screening thresholds.";
    }
}
