using MarketViewer.Contracts.Enums;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Models.Strategy;

[ExcludeFromCodeCoverage]
public class StrategyExitSettings
{
    public required Exit StopLoss { get; init; }
    public required Exit TakeProfit { get; init; }
    public List<string>? ConditionalExit { get; init; }
    public required TimedExit TimedExit { get; init; }
}

[ExcludeFromCodeCoverage]
public class Exit
{
    public ExitCandleType CandleType { get; init; }
    public PriceActionType PriceActionType { get; init; }
    public ExitValueType Type { get; init; }
    public float Value { get; init; }
}

[ExcludeFromCodeCoverage]
public class TimedExit
{
    public bool? AvoidOvernight { get; init; } // Exit before market close to avoid holding overnight
    public required Timeframe Timeframe { get; init; }
}
