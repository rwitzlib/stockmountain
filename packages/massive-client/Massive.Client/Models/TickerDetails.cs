using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Massive.Client.Models;

[ExcludeFromCodeCoverage]
public class TickerDetails
{
    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("address")]
    public Address? Address { get; set; }

    [JsonPropertyName("branding")]
    public Branding? Branding { get; set; }

    [JsonPropertyName("cik")]
    public string? Cik { get; set; }

    [JsonPropertyName("market")]
    public string? Market { get; set; }

    [JsonPropertyName("market_cap")]
    public double MarketCap { get; set; }

    /// <summary>
    /// Free float (shares available for public trading). Not part of the Massive ticker
    /// details payload; populated separately from the /stocks/vX/float endpoint.
    /// </summary>
    [JsonPropertyName("float")]
    public long? Float { get; set; }

    [JsonPropertyName("ticker")]
    public string? Ticker { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("primary_exchange")]
    public string? PrimaryExchange { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}
