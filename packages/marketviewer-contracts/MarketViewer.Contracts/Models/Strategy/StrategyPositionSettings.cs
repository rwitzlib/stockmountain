using MarketViewer.Contracts.Enums;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Models.Strategy;

/// <summary>
/// Information about position sizing and management for backtesting
/// </summary>
[ExcludeFromCodeCoverage]
public class StrategyPositionSettings
{
    public float StartingBalance { get; set; }
    public int MaxConcurrentPositions { get; set; } = 1000;
    public PositionModel Model { get; set; }
    public bool AllowSimultaneous { get; set; } = false; // Allow multiple positions in the same stock at the same time
    public Timeframe Cooldown { get; set; }
}

[ExcludeFromCodeCoverage]
public class PositionModel
{
    public PositionType Type { get; set; }
    public float Size { get; set; }
}
