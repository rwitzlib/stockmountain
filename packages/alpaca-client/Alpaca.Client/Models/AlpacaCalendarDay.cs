using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Alpaca.Client.Models;

/// <summary>
/// One trading day from Alpaca's calendar. Times are Eastern (e.g. "09:30", "16:00");
/// half-days carry an early close.
/// </summary>
[ExcludeFromCodeCoverage]
public class AlpacaCalendarDay
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("open")]
    public string Open { get; set; } = string.Empty;

    [JsonPropertyName("close")]
    public string Close { get; set; } = string.Empty;
}
