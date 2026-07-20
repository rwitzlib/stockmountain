using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Massive.Client.Responses;

[ExcludeFromCodeCoverage]
public class MassiveWebsocketAggregateResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("ev")]
    public string? Event { get; set; }

    [JsonPropertyName("sym")]
    public string? Ticker { get; set; }

    [JsonPropertyName("v")]
    public float Volume { get; set; }

    [JsonPropertyName("av")]
    public float AccumulatedVolume { get; set; }

    [JsonPropertyName("op")]
    public float OpeningPrice { get; set; }

    [JsonPropertyName("vw")]
    public float TickVwap { get; set; }

    [JsonPropertyName("o")]
    public float Open { get; set; }

    [JsonPropertyName("c")]
    public float Close { get; set; }

    [JsonPropertyName("h")]
    public float High { get; set; }

    [JsonPropertyName("l")]
    public float Low { get; set; }

    [JsonPropertyName("a")]
    public float DayVwap { get; set; }

    [JsonPropertyName("z")]
    public float AverageTradeSize { get; set; }

    [JsonPropertyName("s")]
    public long TickStart { get; set; }

    [JsonPropertyName("e")]
    public long TickEnd { get; set; }
}
