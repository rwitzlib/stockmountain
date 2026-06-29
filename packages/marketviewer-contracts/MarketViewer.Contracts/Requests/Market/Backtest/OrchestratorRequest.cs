using MarketViewer.Contracts.Models.Strategy;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Requests.Market.Backtest;

[ExcludeFromCodeCoverage]
public class OrchestratorRequest
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public StrategyPositionSettings PositionSettings { get; set; }
    public StrategyExitSettings ExitSettings { get; set; }
    public StrategyEntrySettings EntrySettings { get; set; }
    public bool DetailedResponse { get; set; } = false;
    public bool IncludeSnapshot { get; set; } = false;
}
