using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Massive.Client.Models;

[ExcludeFromCodeCoverage]
public class Snapshot
{
    [JsonPropertyName("day")]
    public Bar? Day { get; set; }

    [JsonPropertyName("min")]
    public Bar? Minute { get; set; }

    [JsonPropertyName("prevDay")]
    public Bar? PreviousDay { get; set; }

    [JsonPropertyName("ticker")]
    public string? Ticker { get; set; }

    [JsonPropertyName("todaysChange")]
    public float TodaysChange { get; set; }

    [JsonPropertyName("todaysChangePerc")]
    public float TodaysChangePercentage { get; set; }

    [JsonPropertyName("updated")]
    public long Updated { get; set; }
}
