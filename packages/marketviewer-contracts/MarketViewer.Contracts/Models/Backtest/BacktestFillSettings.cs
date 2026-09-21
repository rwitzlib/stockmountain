using MarketViewer.Contracts.Enums.Backtest;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Models.Backtest;

/// <summary>
/// How the backtester prices fills (ADR 0005). Backtests stored before these settings
/// existed have none: they ran with <see cref="Legacy"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public class BacktestFillSettings
{
    public const float MaxSlippagePercent = 10f;

    public BacktestEntryFill EntryFill { get; init; } = BacktestEntryFill.nextBarOpen;

    /// <summary>
    /// Adverse slippage, in percent of price, on market fills: the entry and the
    /// timed/end-of-data exit. Take-profit fills are limit-like and never slip.
    /// </summary>
    public float SlippagePercent { get; init; } = 0f;

    /// <summary>
    /// Adverse slippage, in percent of price, on stop-loss fills, applied after the
    /// trigger/gap-through price. Stops sell into a falling bid, so they slip the most.
    /// </summary>
    public float StopSlippagePercent { get; init; } = 0.5f;

    /// <summary>What every backtest ran with before fill settings existed.</summary>
    public static BacktestFillSettings Legacy => new()
    {
        EntryFill = BacktestEntryFill.signalClose,
        SlippagePercent = 0f,
        StopSlippagePercent = 0f
    };
}
