using TijarahAi.Domain.Entities;

namespace TijarahAi.Application.Common.Interfaces;

public interface IStockDataProvider
{
    Task<StockFinancials> GetFinancialsAsync(string ticker, CancellationToken cancellationToken = default);
}
