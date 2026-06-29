using MarketViewer.Contracts.Models.Backtest;
using MarketViewer.Contracts.Records.Backtest;
using MarketViewer.Contracts.Responses.Market.Backtest;

namespace MarketViewer.Core.Services;

public interface IBacktestRepository
{
    Task<bool> Put(BacktestContextRecord record, IEnumerable<WorkerResponse> entries = null);
    Task<BacktestContextRecord> Get(string id);
    Task<List<BacktestContextRecord>> List(string userId);
    Task<List<WorkerResponse>> GetBacktestResultsFromS3(BacktestContextRecord record);
}