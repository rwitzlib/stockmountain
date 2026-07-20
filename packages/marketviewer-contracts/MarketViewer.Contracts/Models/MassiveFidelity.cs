using MarketViewer.Contracts.Responses.Market;
using Massive.Client.Models;
using Massive.Client.Responses;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Models;

[ExcludeFromCodeCoverage]
public class MassiveFidelity
{
    public string Ticker { get; set; }
    public StocksResponse Data { get; set; }
    public Dictionary<DateTimeOffset, MassiveSnapshotResponse> Snapshots { get; set; }
    public Dictionary<DateTimeOffset, MassiveAggregateResponse> Aggregates { get; set; }
}
