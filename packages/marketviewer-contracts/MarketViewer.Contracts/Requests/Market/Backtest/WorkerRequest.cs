using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Models.Strategy;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Requests.Market.Backtest;

[ExcludeFromCodeCoverage]
public class WorkerRequest
{
    public string BacktestId { get; set; }
    public DateTimeOffset Date { get; set; }
    public StrategyPositionSettings PositionSettings { get; set; }
    public StrategyExitSettings ExitSettings { get; set; }
    public StrategyEntrySettings EntrySettings { get; set; }
}
