using MarketViewer.Contracts.Responses.Market;
using Polygon.Client.Models;
using Polygon.Client.Responses;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Models;

[ExcludeFromCodeCoverage]
public class PolygonFidelity
{
    public string Ticker { get; set; }
    public StocksResponse Data { get; set; }
    public Dictionary<DateTimeOffset, PolygonSnapshotResponse> Snapshots { get; set; }
    public Dictionary<DateTimeOffset, PolygonAggregateResponse> Aggregates { get; set; }
}
