using TijarahAi.Domain.Entities;
using TijarahAi.Domain.Enums;

namespace TijarahAi.Application.Common.Interfaces;

public interface IStockComplianceEngine
{
    ComplianceResult Evaluate(StockFinancials financials, ComplianceStandard standard);
}
