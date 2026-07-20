using System.Text.Json.Serialization;

namespace Massive.Client.Responses;

public abstract class MassiveResponseBase
{
    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }
}
