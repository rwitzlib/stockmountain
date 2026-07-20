using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Massive.Client.Models;

[ExcludeFromCodeCoverage]
public class Bar
{
    [JsonPropertyName("c")]
    public float Close { get; set; }

    [JsonPropertyName("h")]
    public float High { get; set; }

    [JsonPropertyName("l")]
    public float Low { get; set; }

    [JsonPropertyName("n")]
    public int TransactionCount { get; set; }

    [JsonPropertyName("o")]
    public float Open { get; set; }

    [JsonPropertyName("otc")]
    public bool Otc { get; set; }

    [JsonPropertyName("t")]
    public long Timestamp { get; set; }

    [JsonPropertyName("v")]
    public float Volume { get; set; }

    [JsonPropertyName("vw")]
    public float Vwap { get; set; }

    public Bar Clone() => (Bar)MemberwiseClone();
}
