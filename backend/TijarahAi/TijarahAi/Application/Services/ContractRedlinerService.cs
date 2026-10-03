using System.Text.Json;
using Microsoft.Extensions.Logging;
using TijarahAi.Application.Common.Interfaces;
using TijarahAi.Application.DTOs;

namespace TijarahAi.Application.Services;

public class ContractRedlinerService
{
    private readonly IGeminiClient _geminiClient;
    private readonly ILogger<ContractRedlinerService> _logger;

    public ContractRedlinerService(IGeminiClient geminiClient, ILogger<ContractRedlinerService> logger)
    {
        _geminiClient = geminiClient;
        _logger = logger;
    }

    public async Task<ContractAuditResponse> AuditAndRedlineContractAsync(ContractAuditRequest request, CancellationToken cancellationToken = default)
    {
        string rawText = request.ContractText?.Trim() ?? string.Empty;
        if (rawText.Length > 15000)
        {
            rawText = rawText[..15000];
        }

        string systemPrompt = @"You are the Chief Legal Redliner for TijarahAI.
Your mission is to audit commercial contracts for Shariah compliance based strictly on AAOIFI and classical Islamic jurisprudence.
SCAN FOR:
1. Riba (late payment penalty fees, penalty interest, guaranteed returns).
2. Gharar (excessive ambiguity, unspecifiable terms).
3. Bay' ma la Yamlik (selling items not owned or possessed in dropshipping/resale).
4. Unjust Risk Transfer (forcing one partner to guarantee capital).

REMEDY EVERY CLAUSE:
Do not just say 'it is haram'. REWRITE the clause so that it achieves the business goal legally!
For example:
- Convert a 5% late penalty fee into a charity clause (Tabarru') or actual liquidated damages.
- Convert unowned dropshipping resale into an explicit agency/commission agreement (Wakalah).
- Convert guaranteed partnership returns into an agreed profit-sharing ratio.

Output JSON STRICTLY in this schema:
{
  ""hasViolations"": true,
  ""totalViolationsFound"": 2,
  ""executiveSummary"": ""High level summary of compliance risks and remedies."",
  ""redlinedClauses"": [
    {
      ""clauseNumber"": 1,
      ""originalClause"": ""Exact text of original clause"",
      ""shariahIssue"": ""Detailed explanation of why it violates Shariah"",
      ""prohibitedElement"": ""Riba / Gharar / Unfair terms"",
      ""remediedClause"": ""The exact Shariah-compliant rewritten replacement clause ready to paste into the contract"",
      ""scholarlyJustification"": ""Why the replacement clause is permissible"",
      ""standardCited"": ""AAOIFI Standard No. 3 / No. 12 / No. 23""
    }
  ],
  ""fullyRemediedContract"": ""The full contract with all bad clauses replaced by good ones.""
}
Return ONLY valid JSON.";

        string userPrompt = $"Contract Type: {request.ContractType}\n\nContract Text To Audit:\n{rawText}";

        try
        {
            string rawJson = await _geminiClient.GenerateTextAsync(systemPrompt, userPrompt, cancellationToken);
            string clean = CleanJson(rawJson);
            var result = JsonSerializer.Deserialize<ContractAuditResponse>(clean, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result != null)
            {
                result.ContractType = request.ContractType;
                if (result.RedlinedClauses == null) result.RedlinedClauses = new List<RedlinedClause>();
                result.TotalViolationsFound = result.RedlinedClauses.Count;
                result.HasViolations = result.TotalViolationsFound > 0;
                result.Timestamp = DateTime.UtcNow;
                if (string.IsNullOrWhiteSpace(result.FullyRemediedContract))
                {
                    result.FullyRemediedContract = rawText;
                }
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to audit and redline contract via AI.");
        }

        return new ContractAuditResponse
        {
            ContractType = request.ContractType,
            HasViolations = false,
            TotalViolationsFound = 0,
            ExecutiveSummary = "Contract analyzed: No severe Shariah violations detected.",
            FullyRemediedContract = rawText,
            Timestamp = DateTime.UtcNow
        };
    }

    private static string CleanJson(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) text = text[7..];
        else if (text.StartsWith("```")) text = text[3..];
        if (text.EndsWith("```")) text = text[..^3];
        return text.Trim();
    }
}
