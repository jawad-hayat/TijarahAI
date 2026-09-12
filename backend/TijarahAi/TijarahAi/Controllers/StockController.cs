using Microsoft.AspNetCore.Mvc;
using TijarahAi.Application.Common.Interfaces;
using TijarahAi.Application.DTOs;
using TijarahAi.Domain.Entities;
using TijarahAi.Domain.Enums;

namespace TijarahAi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StockController : ControllerBase
{
    private readonly IStockDataProvider _stockDataProvider;
    private readonly IStockComplianceEngine _complianceEngine;
    private readonly IGeminiClient _geminiClient;
    private readonly ILogger<StockController> _logger;

    public StockController(
        IStockDataProvider stockDataProvider,
        IStockComplianceEngine complianceEngine,
        IGeminiClient geminiClient,
        ILogger<StockController> logger)
    {
        _stockDataProvider = stockDataProvider;
        _complianceEngine = complianceEngine;
        _geminiClient = geminiClient;
        _logger = logger;
    }

    [HttpPost("check")]
    public async Task<ActionResult<StockCheckResponse>> CheckStockCompliance([FromBody] StockCheckRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Ticker))
            return BadRequest(new { error = "Stock ticker must be provided." });

        _logger.LogInformation("Auditing ticker '{Ticker}' against both Strict and AAOIFI standards", request.Ticker);

        // 1. Fetch live or calibrated financial metrics
        var financials = await _stockDataProvider.GetFinancialsAsync(request.Ticker, cancellationToken);

        // 2. Execute deterministic compliance calculation (No LLM hallucinations for math)
        var strictResult = _complianceEngine.Evaluate(financials, ComplianceStandard.Strict);
        var aaoifiResult = _complianceEngine.Evaluate(financials, ComplianceStandard.Aaoifi);

        // 3. Generate an independent explanation for each deterministic assessment
        var strictAuditTask = GenerateAiExplanationAsync(financials, strictResult, cancellationToken);
        var aaoifiAuditTask = GenerateAiExplanationAsync(financials, aaoifiResult, cancellationToken);
        await Task.WhenAll(strictAuditTask, aaoifiAuditTask);

        string strictAudit = await strictAuditTask;
        string aaoifiAudit = await aaoifiAuditTask;
        strictResult.AiExecutiveAudit = strictAudit;
        aaoifiResult.AiExecutiveAudit = aaoifiAudit;

        var response = new StockCheckResponse
        {
            Ticker = financials.Ticker,
            CompanyName = financials.CompanyName,
            Sector = financials.Sector,
            CurrentPrice = financials.CurrentPrice,
            MarketCap = financials.MarketCapitalization,
            StrictStandard = BuildAssessment(strictResult),
            AaoifiStandard = BuildAssessment(aaoifiResult),
            Timestamp = DateTime.UtcNow
        };

        return Ok(response);
    }

    private static StockComplianceAssessment BuildAssessment(ComplianceResult result)
    {
        return new StockComplianceAssessment
        {
            StandardApplied = result.StandardApplied,
            Status = result.Status,
            Rules = result.RuleEvaluations,
            DividendPurificationPercentage = result.PurificationPercentage,
            AiExecutiveAudit = result.AiExecutiveAudit
        };
    }

    private async Task<string> GenerateAiExplanationAsync(StockFinancials f, ComplianceResult result, CancellationToken cancellationToken)
    {
        string prompt = $@"Company: {f.CompanyName} ({f.Ticker})
Sector: {f.Sector}
Market Capitalization: {f.MarketCapitalization:C0}
Total Debt: {f.TotalDebt:C0}
Cash & Short-term Investments: {f.CashAndShortTermInvestments:C0}
Accounts Receivable: {f.AccountsReceivable:C0}
Total Revenue: {f.TotalRevenue:C0}
Non-Compliant / Interest Revenue: {f.NonCompliantRevenue:C0}

Applied Standard: {result.StandardApplied}
Overall Status: {result.Status}
Dividend Purification Required: {result.PurificationPercentage}%

Rule Breakdown:
{string.Join("\n", result.RuleEvaluations.Select(r => $"- {r.RuleName}: {r.CalculatedValue}% (Allowed: <={r.AllowedThreshold}%) => {(r.Passed ? "PASS" : "FAIL")}"))}";

        string systemPrompt = @"You are the Chief Shariah Auditor for TijarahAI.
Explain the stock screening results succinctly and authoritatively.
Explain precisely WHY the company passed or failed the given standard based on the financial calculations provided.
If purification is required, explain how an investor should donate that fraction of their dividend.
Do not invent or contradict the provided numbers.";

        try
        {
            return await _geminiClient.GenerateTextAsync(systemPrompt, prompt, cancellationToken);
        }
        catch
        {
            return result.SummaryText;
        }
    }
}
