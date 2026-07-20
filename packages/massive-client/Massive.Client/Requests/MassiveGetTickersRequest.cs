using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Massive.Client.Requests;

[ExcludeFromCodeCoverage]
public class MassiveGetTickersRequest
{
    [JsonPropertyName("ticker")]
    public string? Ticker { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("market")]
    public string? Market { get; set; }

    [JsonPropertyName("exchange")]
    public string? Exchange { get; set; }

    [JsonPropertyName("cusip")]
    public string? Cusip { get; set; }

    [JsonPropertyName("cik")]
    public string? Cik { get; set; }

    [JsonPropertyName("date")]
    public DateTime? Date { get; set; }

    [JsonPropertyName("search")]
    public string? Search { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; } = true;

    [JsonPropertyName("limit")]
    public int Limit { get; set; } = 1000;

    [JsonPropertyName("order")]
    public string Order { get; set; } = "asc";

    [JsonPropertyName("sort")]
    public string? Sort { get; set; }
}
