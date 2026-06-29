using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models.Strategy;
using MarketViewer.Contracts.Records.Strategy;

namespace MarketViewer.Core.Services;

public interface IStrategyRepository
{
    Task<StrategyDto> Create(StrategyDto strategy);
    Task<StrategyDto> Get(string id);
    Task<IEnumerable<StrategyDto>> ListByUser(string userId = null);
    Task<IEnumerable<StrategyDto>> ListByVisibility(VisibilityType visibility);
    Task<IEnumerable<StrategyEntrySettings>> ListUniqueActiveStrategies();
    Task<StrategyDto> Update(StrategyDto strategy, StrategyDto oldStrategy);
    Task<bool> Delete(StrategyDto strategy);
    
    // Strategy State & Balance History
    Task<StrategyStateRecord> GetState(string strategyId);
    Task<IEnumerable<BalanceHistoryRecord>> GetBalanceHistory(string strategyId, string startDate, string endDate);
}