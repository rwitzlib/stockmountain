using MarketViewer.Contracts.Enums;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Models.Strategy;

[ExcludeFromCodeCoverage]
public class StrategyExitSettings
{
    public Exit StopLoss { get; set; }
    public Exit TakeProfit { get; set; }
    public List<string> ConditionalExit { get; set; }
    public TimedExit TimedExit { get; set; }
}

[ExcludeFromCodeCoverage]
public class Exit
{
    public ExitCandleType CandleType { get; set; }
    public PriceActionType PriceActionType { get; set; }
    public ExitValueType Type { get; set; }
    public float Value { get; set; }
}

[ExcludeFromCodeCoverage]
public class TimedExit
{
    public bool AvoidOvernight { get; set; } // Exit before market close to avoid holding overnight
    public Timeframe Timeframe { get; set; }
}
