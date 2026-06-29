using MarketViewer.Contracts.Enums;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace MarketDataAggregator;

[ExcludeFromCodeCoverage]
public class MarketDataAggregatorRequest
{
    public string? Type { get; set; }
    public int Multiplier { get; set; } = 1;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Timespan Timespan { get; set; }

    public DateTimeOffset Date { get; set; }
    public string? RunId { get; set; }
    public string Source { get; set; } = "manual";
    public bool Overwrite { get; set; }
}
