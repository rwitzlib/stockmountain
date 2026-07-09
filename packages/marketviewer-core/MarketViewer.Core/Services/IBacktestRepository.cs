using MarketViewer.Contracts.Models.Backtest;
using MarketViewer.Contracts.Records.Backtest;
using MarketViewer.Contracts.Responses.Market.Backtest;

namespace MarketViewer.Core.Services;

public interface IBacktestRepository
{
    Task<bool> Put(BacktestContextRecord record, IEnumerable<WorkerResponse> entries = null);
    Task<bool> PutCompleted(
        BacktestContextRecord record,
        BacktestResultResponse portfolio,
        IEnumerable<WorkerResponse> universe);
    Task<BacktestContextRecord> Get(string id);
    Task<List<BacktestContextRecord>> List(string userId);
    Task<BacktestResultResponse> GetPortfolioFromS3(BacktestContextRecord record);
    Task<List<WorkerResponse>> GetUniverseFromS3(BacktestContextRecord record);

    /// <summary>
    /// Legacy alias for <see cref="GetUniverseFromS3"/>. Prefer the universe-named method.
    /// </summary>
    Task<List<WorkerResponse>> GetBacktestResultsFromS3(BacktestContextRecord record);
}
