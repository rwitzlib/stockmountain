using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace MarketViewer.Contracts.Requests.Management.Scanner;

[ExcludeFromCodeCoverage]
public class ScannerUpdateRequest : ScannerCreateRequest
{
    // The id comes from the route; JsonIgnore keeps request bodies from setting it
    // (System.Text.Json does not honor [IgnoreDataMember]).
    [JsonIgnore]
    public string Id { get; set; }
}
