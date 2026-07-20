using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Massive.Client.Models;

namespace Massive.Client.Responses;

[ExcludeFromCodeCoverage]
public class MassiveAggregateResponse : MassiveAggregateBaseResponse
{
    [JsonPropertyName("ticker")]
    public string? Ticker { get; set; }

    [JsonPropertyName("results")]
    public IEnumerable<Bar> Results { get; set; } = [];
}
