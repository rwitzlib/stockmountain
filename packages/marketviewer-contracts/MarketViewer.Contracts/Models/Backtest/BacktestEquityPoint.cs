using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Models.Backtest;

/// <summary>
/// One trading day's portfolio snapshot for equity-curve and drawdown calculations.
/// </summary>
[ExcludeFromCodeCoverage]
public class BacktestEquityPoint
{
    public DateTimeOffset Date { get; set; }
    public float StartCash { get; set; }
    public float EndCash { get; set; }
    public float TotalBalance { get; set; }
    public int OpenPositions { get; set; }
    public int MaxConcurrentPositions { get; set; }
    public float DayProfit { get; set; }
    public int TradesTaken { get; set; }

    /// <summary>
    /// Entry signals with a valid outcome that the simulation evaluated this day. Excludes
    /// policy skips (duplicate ticker, cooldown), so SignalsSeen == TradesTaken +
    /// SkippedFunds + SkippedConcurrency. Zero on results persisted before this field existed.
    /// </summary>
    public int SignalsSeen { get; set; }

    /// <summary>Signals not taken because cash was below the position size.</summary>
    public int SkippedFunds { get; set; }

    /// <summary>
    /// Signals not taken because the max-concurrent-positions cap was hit. When a signal
    /// fails both constraints it counts here, not in SkippedFunds.
    /// </summary>
    public int SkippedConcurrency { get; set; }
}
