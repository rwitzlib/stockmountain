using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Models.Backtest;

[ExcludeFromCodeCoverage]
public class BacktestEntryStats
{
    /// <summary>
    /// Total capital available at the end of the run (cash plus any marked positions).
    /// </summary>
    public float EndBalance { get; set; }

    /// <summary>
    /// Net change in balance relative to the starting capital.
    /// </summary>
    public float BalanceChange { get; set; }

    /// <summary>
    /// Fraction of closed trades that finished with positive profit.
    /// </summary>
    public float WinRatio { get; set; }

    /// <summary>
    /// Average profit of winning trades (in currency units).
    /// </summary>
    public float AvgWin { get; set; }

    /// <summary>
    /// Average loss of losing trades (in currency units).
    /// </summary>
    public float AvgLoss { get; set; }

    /// <summary>
    /// Peak number of concurrent open positions during the backtest.
    /// </summary>
    public float MaxConcurrentPositions { get; set; }

    /// <summary>
    /// Total number of trade entries taken.
    /// </summary>
    public int TotalTradesTaken { get; set; }

    /// <summary>
    /// Mean of the day-over-day return series for the strategy.
    /// </summary>
    public float AverageDailyReturn { get; set; }

    /// <summary>
    /// Sample standard deviation of the daily returns.
    /// </summary>
    public float DailyReturnStdDev { get; set; }

    /// <summary>
    /// Annualized Sharpe ratio assuming 252 trading days and zero risk-free rate.
    /// </summary>
    public float SharpeRatio { get; set; }

    /// <summary>
    /// Largest peak-to-trough percentage drop observed in the equity curve.
    /// </summary>
    public float MaxDrawdown { get; set; }

    /// <summary>
    /// Ratio of aggregated winning trade profits to aggregated losing trade losses.
    /// </summary>
    public float ProfitFactor { get; set; }
}
