using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Records.Strategy;

/// <summary>
/// Daily balance snapshot for a strategy.
/// Stored as time-series items for efficient historical queries.
/// DynamoDB: PK = BOT#{StrategyId}, SK = BALANCE#{YYYY-MM-DD}
/// </summary>
[ExcludeFromCodeCoverage]
public class BalanceHistoryRecord
{
    /// <summary>
    /// The strategy ID this balance belongs to.
    /// </summary>
    public string StrategyId { get; set; }

    /// <summary>
    /// The date this snapshot represents (YYYY-MM-DD format).
    /// </summary>
    public string Date { get; set; }

    /// <summary>
    /// Available cash after opens/closes at end of day.
    /// </summary>
    public decimal CashBalance { get; set; }

    /// <summary>
    /// Total cost basis of all open positions at snapshot time.
    /// </summary>
    public decimal TotalEntryCost { get; set; }

    /// <summary>
    /// Mark-to-market P/L of open positions at snapshot time.
    /// </summary>
    public decimal UnrealizedPnl { get; set; }

    /// <summary>
    /// Current market value of open positions (TotalEntryCost + UnrealizedPnl).
    /// </summary>
    public decimal PositionValue { get; set; }

    /// <summary>
    /// CashBalance + PositionValue at snapshot time.
    /// </summary>
    public decimal CurrentBalance { get; set; }

    /// <summary>
    /// Number of open positions at snapshot time.
    /// </summary>
    public int OpenPositionsCount { get; set; }

    /// <summary>
    /// Unix timestamp when this snapshot was recorded.
    /// </summary>
    public long RecordedAt { get; set; }

    /// <summary>
    /// Reason for the snapshot (e.g., "close", "daily", "manual").
    /// </summary>
    public string SnapshotType { get; set; }
}

