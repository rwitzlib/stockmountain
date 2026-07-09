using MarketViewer.Contracts.Enums.Backtest;
using MarketViewer.Contracts.Requests.Market.Backtest;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Records.Backtest;

/// <summary>
/// Database record for storing information about backtest context.
/// </summary>
[ExcludeFromCodeCoverage]
public class BacktestContextRecord
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public BacktestStatus Status { get; set; }
    public string CreatedAt { get; set; }
    public float CreditsUsed { get; set; }
    public float HoldProfit { get; set; }
    public float HighProfit { get; set; }
    public float ConditionalProfit { get; set; }
    public string Start { get; set; }
    public string End { get; set; }
    public BacktestCreateRequest Request { get; set; }

    /// <summary>
    /// S3 key for the unconstrained trade universe (worker results).
    /// </summary>
    public string S3ObjectName { get; set; }

    /// <summary>
    /// S3 key for the capital-constrained portfolio outcome (stats + equity + taken trades).
    /// </summary>
    public string PortfolioS3ObjectName { get; set; }

    public List<string> Errors { get; set; }
    public float DurationSeconds { get; set; }

    /// <summary>
    /// Serialized JSON of BacktestEntryStatsSummary for the "hold" strategy.
    /// Stored in database for quick access in list views without S3 fetches.
    /// </summary>
    public string? HoldStatsJson { get; set; }

    /// <summary>
    /// Serialized JSON of BacktestEntryStatsSummary for the "high" strategy.
    /// Stored in database for quick access in list views without S3 fetches.
    /// </summary>
    public string? HighStatsJson { get; set; }
}
