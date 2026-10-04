using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TijarahAi.Application.Common.Interfaces;
using TijarahAi.Application.DTOs;

namespace TijarahAi.Application.Services;

public class ContractRedlinerService
{
    private readonly IGeminiClient _geminiClient;
    private readonly IGroqClient _groqClient;
    private readonly ILogger<ContractRedlinerService> _logger;

    public ContractRedlinerService(
        IGeminiClient geminiClient,
        IGroqClient groqClient,
        ILogger<ContractRedlinerService> logger)
    {
        _geminiClient = geminiClient;
        _groqClient = groqClient;
        _logger = logger;
    }

    public async Task<ContractAuditResponse> AuditAndRedlineContractAsync(ContractAuditRequest request, CancellationToken cancellationToken = default)
    {
        string rawText = request.ContractText?.Trim() ?? string.Empty;
        if (rawText.Length > 15000)
        {
            rawText = rawText[..15000];
        }

        if (string.IsNullOrWhiteSpace(rawText))
        {
            return new ContractAuditResponse
            {
                ContractType = request.ContractType,
                HasViolations = false,
                TotalViolationsFound = 0,
                ExecutiveSummary = "No contract text provided for audit.",
                FullyRemediedContract = string.Empty,
                Timestamp = DateTime.UtcNow
            };
        }

        // 1. Run deterministic Shariah rule engine
        var ruleAudit = EvaluateDeterministicRules(rawText, request.ContractType);

        // 2. Attempt AI generation if available (with Groq backup on 429)
        ContractAuditResponse? aiAudit = null;
        try
        {
            aiAudit = await AuditWithGeminiAsync(rawText, request.ContractType, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Gemini AI contract audit deferred or failed ({Message}). Attempting Groq Cloud ({Model}) backup.", ex.Message, _groqClient.ModelName);
            try
            {
                aiAudit = await AuditWithGroqAsync(rawText, request.ContractType, cancellationToken);
            }
            catch (Exception groqEx)
            {
                _logger.LogError(groqEx, "Groq Cloud contract audit backup also failed. Using deterministic Shariah audit engine.");
            }
        }

        // 3. Resolve results: If AI succeeded and detected violations, prefer or merge
        if (aiAudit != null && aiAudit.HasViolations && aiAudit.RedlinedClauses.Count >= ruleAudit.TotalViolationsFound)
        {
            return aiAudit;
        }

        // 4. If rule engine detected violations, return rule engine result
        if (ruleAudit.HasViolations)
        {
            return ruleAudit;
        }

        // 5. Fallback to AI result if clean, or rule audit if clean
        return aiAudit ?? ruleAudit;
    }

    private static string GetAuditSystemPrompt()
    {
        return @"You are the Chief Legal Redliner for TijarahAI.
Your mission is to audit commercial contracts for Shariah compliance based strictly on AAOIFI and classical Islamic jurisprudence.
SCAN FOR:
1. Riba (interest rates, late payment penalty fees, penalty interest, guaranteed financial returns).
2. Gharar (excessive ambiguity, unilateral term changes without prior notice, unspecifiable terms).
3. Bay' ma la Yamlik (selling items not owned or possessed in dropshipping/resale).
4. Unjust Terms & Dhulm (extrajudicial seizure of home/assets, waiving usury protections, forcing one partner to guarantee capital).

REMEDY EVERY CLAUSE:
Do not just say 'it is haram'. REWRITE the clause so that it achieves the business goal legally!
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
      ""prohibitedElement"": ""Riba / Gharar / Dhulm / Unfair terms"",
      ""remediedClause"": ""The exact Shariah-compliant rewritten replacement clause ready to paste into the contract"",
      ""scholarlyJustification"": ""Why the replacement clause is permissible"",
      ""standardCited"": ""AAOIFI Standard No. 3 / No. 12 / No. 23""
    }
  ],
  ""fullyRemediedContract"": ""The full contract with all bad clauses replaced by good ones.""
}
Return ONLY valid JSON.";
    }

    private async Task<ContractAuditResponse?> AuditWithGeminiAsync(string rawText, string contractType, CancellationToken cancellationToken)
    {
        string systemPrompt = GetAuditSystemPrompt();
        string userPrompt = $"Contract Type: {contractType}\n\nContract Text To Audit:\n{rawText}";

        string rawJson;
        try
        {
            rawJson = await _geminiClient.GenerateTextAsync(systemPrompt, userPrompt, cancellationToken);
        }
        catch (Exception ex) when (IsRateLimitOrKeyError(ex))
        {
            _logger.LogWarning("Gemini returned 429 / rate limit ({Message}). Falling back to Groq Cloud ({Model}) for contract audit.", ex.Message, _groqClient.ModelName);
            return await AuditWithGroqAsync(rawText, contractType, cancellationToken);
        }

        return ParseAuditResponse(rawJson, rawText, contractType);
    }

    private async Task<ContractAuditResponse?> AuditWithGroqAsync(string rawText, string contractType, CancellationToken cancellationToken)
    {
        string systemPrompt = GetAuditSystemPrompt();
        string userPrompt = $"Contract Type: {contractType}\n\nContract Text To Audit:\n{rawText}";

        _logger.LogInformation("Auditing contract with Groq Cloud ({Model}).", _groqClient.ModelName);
        string rawJson = await _groqClient.GenerateTextAsync(systemPrompt, userPrompt, cancellationToken);
        return ParseAuditResponse(rawJson, rawText, contractType);
    }

    private static ContractAuditResponse? ParseAuditResponse(string rawJson, string rawText, string contractType)
    {
        string clean = CleanJson(rawJson);
        var result = JsonSerializer.Deserialize<ContractAuditResponse>(clean, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (result != null)
        {
            result.ContractType = contractType;
            if (result.RedlinedClauses == null) result.RedlinedClauses = new List<RedlinedClause>();
            result.TotalViolationsFound = result.RedlinedClauses.Count;
            result.HasViolations = result.TotalViolationsFound > 0;
            result.Timestamp = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(result.FullyRemediedContract))
                result.FullyRemediedContract = rawText;
            return result;
        }

        return null;
    }

    private static bool IsRateLimitOrKeyError(Exception ex)
    {
        string msg = ex.Message;
        return msg.Contains("429", StringComparison.Ordinal) ||
               msg.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("GEMINI_API_KEY", StringComparison.OrdinalIgnoreCase) ||
               (ex is HttpRequestException httpEx && httpEx.StatusCode == System.Net.HttpStatusCode.TooManyRequests);
    }

    private static ContractAuditResponse EvaluateDeterministicRules(string contractText, string contractType)
    {
        var redlinedClauses = new List<RedlinedClause>();
        string fullyRemedied = contractText;

        // Split text into candidate sentences/clauses
        var sentences = Regex.Split(contractText, @"(?<=[.!?;\n])\s+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();

        if (sentences.Count == 0)
        {
            sentences = new List<string> { contractText };
        }

        int clauseCounter = 1;

        foreach (var sentence in sentences)
        {
            bool matched = false;

            // 1. Specific Check: Dhulm & Extrajudicial Asset Seizure
            if (!matched && Regex.IsMatch(sentence, @"(?i)(seize.*without\s+court|confiscate.*without\s+court|forfeit.*without\s+court|extrajudicial\s+seizure)"))
            {
                string remedy = "In the event of payment default by a solvent borrower, Lender may pursue lawful recovery through proper judicial channels. Insolvent borrowers facing demonstrated hardship shall be granted reasonable respite (Inzar al-Mu'sir) per Quran 2:280.";

                redlinedClauses.Add(new RedlinedClause
                {
                    ClauseNumber = clauseCounter++,
                    OriginalClause = sentence,
                    ProhibitedElement = "Dhulm & Coercion (Unjust Extrajudicial Asset Seizure)",
                    ShariahIssue = "Extrajudicial seizure of basic shelter or essential assets without judicial process violates the sanctity of private property and mandatory Islamic debt relief ethics (Quran 2:280).",
                    RemediedClause = remedy,
                    ScholarlyJustification = "Islamic law safeguards basic necessities (Daruriyyat) including shelter, requiring judicial oversight and mandatory respite for debtors in distress.",
                    StandardCited = "AAOIFI Standard No. 3 (Default in Payment) & Quran 2:280"
                });

                fullyRemedied = fullyRemedied.Replace(sentence, remedy);
                matched = true;
            }

            // 2. Specific Check: Void Usury Waiver & Consumer Rights Forfeiture
            if (!matched && Regex.IsMatch(sentence, @"(?i)(waives?\s+(all\s+)?consumer\s+protection|waives?\s+(all\s+)?rights\s+under\s+.*usury|waives?\s+legal\s+recourse|waives?\s+all\s+rights)"))
            {
                string remedy = "Both parties agree that this agreement strictly conforms to applicable statutory consumer protection standards and Shariah principles of commercial equity, prohibiting usury, exploitation, and unconscionable covenants.";

                redlinedClauses.Add(new RedlinedClause
                {
                    ClauseNumber = clauseCounter++,
                    OriginalClause = sentence,
                    ProhibitedElement = "Shart Batil (Void Condition Waiving Protections Against Usury)",
                    ShariahIssue = "A contractual condition compelling a borrower to waive statutory consumer protections or anti-usury rights is a void condition (Shart Batil) that contradicts public interest and Shariah justice.",
                    RemediedClause = remedy,
                    ScholarlyJustification = "The Prophet (ﷺ) declared: 'Every condition that is not in accordance with the Book of Allah is void' (Sahih al-Bukhari). Statutory safeguards against oppression cannot be waived.",
                    StandardCited = "Legal Maxim: Al-Shart al-Batil La Yulzam (Void Conditions Are Unenforceable)"
                });

                fullyRemedied = fullyRemedied.Replace(sentence, remedy);
                matched = true;
            }

            // 3. Specific Check: Gharar Fahish & Unilateral Terms/Fee Alteration
            if (!matched && Regex.IsMatch(sentence, @"(?i)(without\s+(prior\s+)?notice|at\s+any\s+time\s+without\s+(prior\s+)?notice|sole\s+discretion\s+without\s+notice|arbitrarily\s+changed|unilaterally\s+modify|changed\s+by\s+\w+\s+at\s+any\s+time)"))
            {
                string remedy = "All administrative fees, service charges, and contractual conditions must be fixed, clearly itemized, and mutually agreed upon in writing at contract inception, and cannot be modified unilaterally by either party.";

                redlinedClauses.Add(new RedlinedClause
                {
                    ClauseNumber = clauseCounter++,
                    OriginalClause = sentence,
                    ProhibitedElement = "Gharar Fahish (Excessive Uncertainty & Unilateral Discretion)",
                    ShariahIssue = "Unilateral rights to alter fees, charges, or contract terms without prior mutual consent introduce major uncertainty (Gharar Fahish), rendering the contract legally defective (Fasid) under Islamic jurisprudence.",
                    RemediedClause = remedy,
                    ScholarlyJustification = "Price, obligations, and terms must be fully determinate (Ma'lum) and based on mutual consent (Taradin) to avoid exploitation and disputes.",
                    StandardCited = "AAOIFI Standard No. 31 (Stipulations in Contracts)"
                });

                fullyRemedied = fullyRemedied.Replace(sentence, remedy);
                matched = true;
            }

            // 4. Specific Check: Bay' ma la Yamlik (Selling What You Do Not Own)
            if (!matched && Regex.IsMatch(sentence, @"(?i)(prior\s+to\s+purchasing|before\s+buying\s+or\s+possessing|sell.*prior\s+to\s+acquiring\s+title|resell.*without\s+ownership|inventory.*prior\s+to\s+purchasing)"))
            {
                string remedy = "Reseller acts as an authorized commercial agent (Wakil bi al-Ujrah) marketing goods on behalf of the Supplier for an agreed commission, or transactions are structured under Salam/Istisna'a forward-sale rules with title conveyed upon procurement.";

                redlinedClauses.Add(new RedlinedClause
                {
                    ClauseNumber = clauseCounter++,
                    OriginalClause = sentence,
                    ProhibitedElement = "Bay' ma la Yamlik (Selling Goods Prior to Ownership/Possession)",
                    ShariahIssue = "Executing a binding contract of sale on specific goods before taking constructive possession or acquiring legal title violates the explicit Hadith: 'Do not sell what you do not own' (Sunan Abi Dawud 3503).",
                    RemediedClause = remedy,
                    ScholarlyJustification = "A valid agency (Wakalah) allows an intermediary to broker transactions and receive fees without assuming unowned ownership liabilities.",
                    StandardCited = "AAOIFI Standard No. 23 (Agency / Wakalah) & Standard No. 10 (Salam)"
                });

                fullyRemedied = fullyRemedied.Replace(sentence, remedy);
                matched = true;
            }

            // 5. Specific Check: Prohibited Capital or Return Guarantees in Partnerships
            if (!matched && Regex.IsMatch(sentence, @"(?i)(fixed\s+guaranteed\s+.*return|guarantees?\s+(a\s+)?\d+%\s+return|guarantees?\s+to\s+return\s+100%\s+of.*investment|guarantees?\s+full\s+return\s+of\s+(original\s+)?(principal|capital)|regardless\s+of\s+actual\s+commercial\s+performance|regardless\s+of\s+market\s+(outcomes|losses))"))
            {
                string remedy = "Profits shall be shared strictly on an agreed profit-sharing ratio based on actual realized returns, while capital losses shall be borne by capital providers in proportion to their contributions, with no partner guaranteeing capital against normal market loss.";

                redlinedClauses.Add(new RedlinedClause
                {
                    ClauseNumber = clauseCounter++,
                    OriginalClause = sentence,
                    ProhibitedElement = "Daman al-Ra's al-Mal (Prohibited Capital Guarantee in Partnership)",
                    ShariahIssue = "In Mudarabah and Musharakah partnerships, guaranteeing investor capital or fixing a percentage return on capital is strictly prohibited because it shifts commercial risk completely and converts the equity relationship into an interest-bearing loan.",
                    RemediedClause = remedy,
                    ScholarlyJustification = "The legal maxim states: 'Al-Kharaj bi al-Daman' (Entitlement to profit is accompanied by liability for loss). Risk and reward cannot be decoupled in partnership contracts.",
                    StandardCited = "AAOIFI Standard No. 12 (Sharika) & Standard No. 13 (Mudaraba)"
                });

                fullyRemedied = fullyRemedied.Replace(sentence, remedy);
                matched = true;
            }

            // 6. General Check: Riba & Usurious Interest / Late Penalty Fees
            if (!matched && Regex.IsMatch(sentence, @"(?i)\b(interest\s+rate|annual\s+interest|monthly\s+interest|interest\s+of\s+\d+%|\b\d+%\s+interest|late\s+(payment\s+)?(fee|penalty)|penalty\s+fee|compounding\s+monthly|\bapr\b|\busury\b|\b\d+%\s+per\s+month|\b\d+%\s+per\s+week)"))
            {
                string remedy = contractType.Contains("Loan", StringComparison.OrdinalIgnoreCase) || contractType.Contains("Borrow", StringComparison.OrdinalIgnoreCase)
                    ? "Borrower shall repay the principal amount of financing without interest under a benevolent loan (Qard Hasan), or parties may structure the transaction as an asset-backed cost-plus sale (Murabahah) with an agreed fixed markup."
                    : "In the event of delayed payment by a solvent debtor, the debtor agrees to pay a late penalty fee of a predetermined reasonable sum to be channeled directly to an independent accredited charity (Tabarru') under AAOIFI rules, with 0% retained by creditor.";

                redlinedClauses.Add(new RedlinedClause
                {
                    ClauseNumber = clauseCounter++,
                    OriginalClause = sentence,
                    ProhibitedElement = "Riba (Prohibited Usury & Delay Interest)",
                    ShariahIssue = "Charging, compounding, or stipulating interest or punitive delay fees on debt constitutes prohibited Riba (Riba an-Nasi'ah) under Quran 2:275 and universal scholarly consensus. A loan in Shariah is strictly gratuitous and cannot generate financial return.",
                    RemediedClause = remedy,
                    ScholarlyJustification = "Loans in Islamic jurisprudence must be interest-free (Qard Hasan). Commercial delay damages must be designated exclusively for charity (Tabarru') to deter willful default without enriching the creditor.",
                    StandardCited = "AAOIFI Standard No. 3 (Default in Payment) & Standard No. 19 (Qard)"
                });

                fullyRemedied = fullyRemedied.Replace(sentence, remedy);
                matched = true;
            }
        }

        bool hasViolations = redlinedClauses.Count > 0;
        string executiveSummary = hasViolations
            ? $"Severe Shariah violations detected & remediated: {redlinedClauses.Count} prohibited clause(s) identified (including {string.Join(", ", redlinedClauses.Select(c => c.ProhibitedElement).Distinct())}). The agreement has been autonomously restructured into a Shariah-compliant contract under AAOIFI Standards."
            : "Contract analyzed: No severe Shariah violations detected under current screening rules.";

        return new ContractAuditResponse
        {
            ContractType = contractType,
            HasViolations = hasViolations,
            TotalViolationsFound = redlinedClauses.Count,
            RedlinedClauses = redlinedClauses,
            FullyRemediedContract = fullyRemedied,
            ExecutiveSummary = executiveSummary,
            Timestamp = DateTime.UtcNow
        };
    }

    private static string CleanJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        text = text.Trim();

        int firstBrace = text.IndexOf('{');
        int lastBrace = text.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return text.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();
        }

        if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) text = text[7..];
        else if (text.StartsWith("```")) text = text[3..];
        if (text.EndsWith("```")) text = text[..^3];
        return text.Trim();
    }
}
