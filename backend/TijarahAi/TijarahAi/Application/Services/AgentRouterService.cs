using System.Text.RegularExpressions;
using TijarahAi.Domain.Enums;

namespace TijarahAi.Application.Services;

public class AgentRouterService
{
    private static readonly Regex StockRegex = new(@"\b(ticker|stock|shares|equity|aaoifi|screener|dividend|purification|balance sheet|[A-Z]{2,5})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FiqhRegex = new(@"\b(halal|haram|permissible|allowed|fiqh|shariah|hanafi|hadith|buyu|contract|riba|gharar|dropshipping|murabaha|musharakah|ijarah|salam|agency|commission)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public AgentIntent DetectIntent(string userQuery)
    {
        if (string.IsNullOrWhiteSpace(userQuery))
            return AgentIntent.GeneralBusinessAdvice;

        bool hasStockSignal = StockRegex.IsMatch(userQuery);
        bool hasFiqhSignal = FiqhRegex.IsMatch(userQuery);

        if (hasStockSignal && !hasFiqhSignal)
            return AgentIntent.StockScreener;

        return AgentIntent.FiqhQuery;
    }
}
