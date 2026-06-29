using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Records;

namespace MarketViewer.Core.Services;

public interface ITradeRepository
{
    Task<bool> Put(TradeRecord trade);
    Task<TradeRecord> Get(string id);
    Task<IEnumerable<TradeRecord>> ListTradesByUser(string userId, TradeType? type = null, TradeStatus? status = null);
    Task<IEnumerable<TradeRecord>> ListTradesByStrategy(string strategyId, TradeType? type = null, TradeStatus? status = null);
}