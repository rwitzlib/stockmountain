using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Massive.Client.Models;

[ExcludeFromCodeCoverage]
public class StockFloat
{
    [JsonPropertyName("ticker")]
    public string? Ticker { get; set; }

    [JsonPropertyName("effective_date")]
    public string? EffectiveDate { get; set; }

    [JsonPropertyName("free_float")]
    public long FreeFloat { get; set; }

    [JsonPropertyName("free_float_percent")]
    public double FreeFloatPercent { get; set; }
}
