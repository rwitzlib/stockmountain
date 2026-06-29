using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Models.Backtest;

/// <summary>
/// Summary statistics for a backtest entry, used in list views to avoid expensive S3 fetches.
/// Contains key performance metrics extracted from detailed BacktestEntryStats.
/// </summary>
[ExcludeFromCodeCoverage]
public class BacktestEntryStatsSummary
{
    /// <summary>
    /// Fraction of closed trades that finished with positive profit.
    /// </summary>
    public float WinRatio { get; set; }

    /// <summary>
    /// Ratio of aggregated winning trade profits to aggregated losing trade losses.
    /// </summary>
    public float ProfitFactor { get; set; }

    /// <summary>
    /// Total number of trade entries taken.
    /// </summary>
    public int TotalTradesTaken { get; set; }

    /// <summary>
    /// Largest peak-to-trough percentage drop observed in the equity curve.
    /// </summary>
    public float MaxDrawdown { get; set; }

    /// <summary>
    /// Annualized Sharpe ratio assuming 252 trading days and zero risk-free rate.
    /// </summary>
    public float SharpeRatio { get; set; }
}

