using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Alpaca.Client.Models;

[ExcludeFromCodeCoverage]
public class AlpacaClock
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("is_open")]
    public bool IsOpen { get; set; }

    [JsonPropertyName("next_open")]
    public DateTimeOffset NextOpen { get; set; }

    [JsonPropertyName("next_close")]
    public DateTimeOffset NextClose { get; set; }
}
