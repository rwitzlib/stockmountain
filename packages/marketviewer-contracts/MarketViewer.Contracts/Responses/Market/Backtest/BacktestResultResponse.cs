using MarketViewer.Contracts.Models.Backtest;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Responses.Market.Backtest;

/// <summary>
/// Portfolio outcome for a completed backtest (stats + equity curve + taken trades).
/// The unconstrained trade universe is available separately via the universe endpoint.
/// </summary>
[ExcludeFromCodeCoverage]
public class BacktestResultResponse
{
    public string Id { get; set; }

    /// <summary>
    /// Given a 2 GB Lambda, 1 second of backtesting will cost $0.0000333.
    /// So 1 credit is equal to $0.0000333.
    /// Assuming 1 day of backtesting will take 120 seconds, each day of backtesting costs 120 credits or $0.0034
    /// </summary>
    public float CreditsUsed { get; set; }

    public BacktestStrategyPortfolio Hold { get; set; }
    public BacktestStrategyPortfolio High { get; set; }

    /// <summary>
    /// Present when a conditional/other exit path was simulated; otherwise null.
    /// </summary>
    public BacktestStrategyPortfolio Other { get; set; }
}
