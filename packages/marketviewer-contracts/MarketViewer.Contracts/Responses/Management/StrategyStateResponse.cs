using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Responses.Management;

/// <summary>
/// Response containing the current state of a strategy.
/// </summary>
[ExcludeFromCodeCoverage]
public class StrategyStateResponse
{
    public string StrategyId { get; set; }
    public decimal CashBalance { get; set; }
    /// <summary>
    /// Total cost basis of all open positions (sum of entry costs).
    /// </summary>
    public decimal TotalEntryCost { get; set; }
    public decimal UnrealizedPnl { get; set; }
    /// <summary>
    /// Current market value of all open positions (TotalEntryCost + UnrealizedPnl).
    /// </summary>
    public decimal PositionValue { get; set; }
    /// <summary>
    /// Total account value (CashBalance + PositionValue).
    /// </summary>
    public decimal CurrentBalance { get; set; }
    public int OpenPositionsCount { get; set; }
    public List<string> OpenTickers { get; set; } = [];
    public Dictionary<string, long> Cooldowns { get; set; } = [];
    public long LastTradeAt { get; set; }
    public long Version { get; set; }
}

