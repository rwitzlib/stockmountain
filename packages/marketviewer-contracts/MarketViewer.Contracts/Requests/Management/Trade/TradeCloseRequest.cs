using MarketViewer.Contracts.Models;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace MarketViewer.Contracts.Requests.Management.Trade;

[ExcludeFromCodeCoverage]
public class TradeCloseRequest
{
    [IgnoreDataMember]
    public string TradeId { get; set; }
    public string ClosedAt { get; set; }
    public float ClosePrice { get; set; }
    public float ClosePosition { get; set; }
    public float Profit { get; set; }
}
