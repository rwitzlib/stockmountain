using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace MarketViewer.Contracts.Requests;

[ExcludeFromCodeCoverage]
public class BaseRequest
{
    [JsonIgnore]
    public string UserId { get; set; }
}
