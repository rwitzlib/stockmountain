using MarketViewer.Contracts.Models.Strategy;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Requests.Market.Backtest;

[ExcludeFromCodeCoverage]
public class BacktestCreateRequest : BaseRequest
{
    public DateTimeOffset Start { get; init; }
    public DateTimeOffset End { get; init; }
    public required StrategyPositionSettings PositionSettings { get; init; }
    public required StrategyExitSettings ExitSettings { get; set; }
    public required StrategyEntrySettings EntrySettings { get; set; }
}
