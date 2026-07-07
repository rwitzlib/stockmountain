using MarketViewer.Contracts.Enums;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Models.Strategy;

/// <summary>
/// Information about position sizing and management for backtesting
/// </summary>
[ExcludeFromCodeCoverage]
public class StrategyPositionSettings
{
    public float StartingBalance { get; init; }
    public int MaxConcurrentPositions { get; init; } = 1000;
    public required PositionModel Model { get; init; }
    public bool AllowSimultaneous { get; init; } = false; // Allow multiple positions in the same stock at the same time
    public required Timeframe Cooldown { get; init; }
}

[ExcludeFromCodeCoverage]
public class PositionModel
{
    public PositionType Type { get; set; }
    public float Size { get; set; }
}
