using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Massive.Client.Models;

namespace Massive.Client.Responses;

[ExcludeFromCodeCoverage]
public class MassiveFloatResponse : MassiveResponseBase
{
    [JsonPropertyName("results")]
    public IEnumerable<StockFloat> Results { get; set; } = [];

    [JsonPropertyName("next_url")]
    public string? NextUrl { get; set; }
}
