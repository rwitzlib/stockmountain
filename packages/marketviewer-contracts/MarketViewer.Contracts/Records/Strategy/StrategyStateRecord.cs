using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Records.Strategy;

/// <summary>
/// Runtime state for a strategy, stored separately from CONFIG to avoid overwrites.
/// DynamoDB: PK = BOT#{StrategyId}, SK = STATE
/// </summary>
[ExcludeFromCodeCoverage]
public class StrategyStateRecord
{
    /// <summary>
    /// The strategy ID this state belongs to.
    /// </summary>
    public string StrategyId { get; set; }

    /// <summary>
    /// Available cash after opens/closes.
    /// </summary>
    public decimal CashBalance { get; set; }

    /// <summary>
    /// Total cost basis of all open positions (sum of entry costs).
    /// Increases when buying, decreases when selling.
    /// </summary>
    public decimal TotalEntryCost { get; set; }

    /// <summary>
    /// Mark-to-market value of open positions minus entry cost.
    /// Updated on-demand or via scheduled refresh.
    /// </summary>
    public decimal UnrealizedPnl { get; set; }

    /// <summary>
    /// Current market value of all open positions.
    /// PositionValue = TotalEntryCost + UnrealizedPnl
    /// </summary>
    public decimal PositionValue => TotalEntryCost + UnrealizedPnl;

    /// <summary>
    /// Total account value including cash and positions.
    /// CurrentBalance = CashBalance + PositionValue = CashBalance + TotalEntryCost + UnrealizedPnl
    /// </summary>
    public decimal CurrentBalance => CashBalance + PositionValue;

    /// <summary>
    /// Number of currently open positions.
    /// </summary>
    public int OpenPositionsCount { get; set; }

    /// <summary>
    /// Set of tickers with currently open positions.
    /// </summary>
    public HashSet<string> OpenTickers { get; set; } = [];

    /// <summary>
    /// Map of ticker to cooldown expiry (Unix seconds).
    /// Ticker cannot be bought again until after this time.
    /// </summary>
    public Dictionary<string, long> Cooldowns { get; set; } = [];

    /// <summary>
    /// Unix seconds of the last trade event (open or close).
    /// </summary>
    public long LastTradeAt { get; set; }

    /// <summary>
    /// Optimistic locking version for conditional updates.
    /// </summary>
    public long Version { get; set; }
}

