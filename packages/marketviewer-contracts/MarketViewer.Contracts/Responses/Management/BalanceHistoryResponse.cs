using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Responses.Management;

/// <summary>
/// Response containing balance history for a strategy.
/// </summary>
[ExcludeFromCodeCoverage]
public class BalanceHistoryResponse
{
    public string StrategyId { get; set; }
    public List<BalanceHistoryEntry> History { get; set; } = [];
}

/// <summary>
/// A single balance history entry.
/// </summary>
[ExcludeFromCodeCoverage]
public class BalanceHistoryEntry
{
    public string Date { get; set; }
    public decimal CashBalance { get; set; }
    /// <summary>
    /// Total cost basis of all open positions at this snapshot.
    /// </summary>
    public decimal TotalEntryCost { get; set; }
    public decimal UnrealizedPnl { get; set; }
    /// <summary>
    /// Current market value of open positions (TotalEntryCost + UnrealizedPnl).
    /// </summary>
    public decimal PositionValue { get; set; }
    /// <summary>
    /// Total account value (CashBalance + PositionValue).
    /// </summary>
    public decimal CurrentBalance { get; set; }
    public int OpenPositionsCount { get; set; }
    public long RecordedAt { get; set; }
    public string SnapshotType { get; set; }
}

