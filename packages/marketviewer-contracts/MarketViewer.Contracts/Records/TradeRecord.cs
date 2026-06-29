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
}
