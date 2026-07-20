using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Massive.Client.Models;

namespace Massive.Client.Responses;

[ExcludeFromCodeCoverage]
public class MassiveTickerDetailsResponse : MassiveResponseBase
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("results")]
    public TickerDetails? TickerDetails { get; set; }
}
