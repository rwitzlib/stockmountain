using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Models.Backtest;

/// <summary>
/// Portfolio outcome for a single exit strategy (hold, high, or other).
/// </summary>
[ExcludeFromCodeCoverage]
public class BacktestStrategyPortfolio
{
    public BacktestEntryStats Stats { get; set; }
    public List<BacktestEquityPoint> Equity { get; set; } = [];
    public List<BacktestExecutedTrade> Trades { get; set; } = [];
}
