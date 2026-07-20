using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Massive.Client.Models;

namespace Massive.Client.Responses;

[ExcludeFromCodeCoverage]
public class MassiveSnapshotResponse : MassiveResponseBase
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("tickers")]
    public IEnumerable<Snapshot> Tickers { get; set; } = [];
}
