using MarketViewer.Contracts.Enums;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Models.Strategy;

[ExcludeFromCodeCoverage]
public class StrategyExitSettings
{
    public required Exit StopLoss { get; init; }
    public required Exit TakeProfit { get; init; }
    public TrailingStop? TrailingStop { get; init; }
    public List<string>? ConditionalExit { get; init; }
    public required TimedExit TimedExit { get; init; }
}

/// <summary>
/// Optional stop that follows the position's high-water mark (ADR 0006). The stop rests
/// <see cref="Value"/> below the highest price seen since entry and only ever ratchets up.
/// It runs alongside the fixed stop loss, which keeps bounding the loss until the trail
/// rises past it.
/// </summary>
[ExcludeFromCodeCoverage]
public class TrailingStop
{
    public ExitValueType Type { get; init; }

    /// <summary>Trail distance: percent of the high-water mark, or position dollars (flat).</summary>
    public float Value { get; init; }

    /// <summary>
    /// Gain over entry (same units as <see cref="Type"/>) the high-water mark must reach
    /// before the trail arms. Null or 0 trails from entry.
    /// </summary>
    public float? Activation { get; init; }
}

[ExcludeFromCodeCoverage]
public class Exit
{
    public ExitValueType Type { get; init; }
    public float Value { get; init; }
}

[ExcludeFromCodeCoverage]
public class TimedExit
{
    public bool? AvoidOvernight { get; init; } // Exit before market close to avoid holding overnight
    public required Timeframe Timeframe { get; init; }
}
