using MarketViewer.Contracts.Enums.Backtest;
using MarketViewer.Contracts.Enums.Strategy;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Records;

[ExcludeFromCodeCoverage]
public class TradeRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; }
    public string StrategyId { get; set; }
    public TradeType Type { get; set; }
    public TradeStatus OrderStatus { get; set; }
    public string Ticker { get; set; }
    public int Shares { get; set; }
    public string OpenedAt { get; set; }
    public string ClosedAt { get; set; }
    public float EntryPrice { get; set; }
    public float ClosePrice { get; set; }
    public float EntryPosition { get; set; }
    public float ClosePosition { get; set; }
    public float Profit { get; set; }

    /// <summary>Why the position was closed. Null while open and on records predating the field.</summary>
    public BacktestExitReason? ExitReason { get; set; }

    /// <summary>Broker order id of the entry fill. Null on internal paper trades.</summary>
    public string EntryOrderId { get; set; }

    /// <summary>Broker order id of the closing fill (market sell, or the backstop if it fired).</summary>
    public string CloseOrderId { get; set; }

    /// <summary>
    /// GTC stop-market disaster backstop resting broker-side while the position is open.
    /// Reconciliation re-places missing backstops keyed off this field.
    /// </summary>
    public string BackstopOrderId { get; set; }
}
