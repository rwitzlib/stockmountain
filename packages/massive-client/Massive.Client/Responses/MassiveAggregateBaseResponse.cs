using System.Text.Json.Serialization;

namespace Massive.Client.Responses;

public class MassiveAggregateBaseResponse : MassiveResponseBase
{
    [JsonPropertyName("resultsCount")]
    public int ResultsCount { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("queryCount")]
    public int QueryCount { get; set; }

    [JsonPropertyName("adjusted")]
    public bool Adjusted { get; set; }

    [JsonPropertyName("next_url")]
    public string? NextUrl { get; set; }
}
