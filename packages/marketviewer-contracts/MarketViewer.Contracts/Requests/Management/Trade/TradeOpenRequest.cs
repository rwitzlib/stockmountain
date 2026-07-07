using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Models;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Requests.Management.Trade;

[ExcludeFromCodeCoverage]
public class TradeOpenRequest
{
    public string StrategyId { get; set; }
    public TradeType Type { get; set; }
    public string Ticker { get; set; }
    public int Shares { get; set; }
    public string OpenedAt { get; set; }
    public float EntryPrice { get; set; }
    public float EntryPosition { get; set; }
}
