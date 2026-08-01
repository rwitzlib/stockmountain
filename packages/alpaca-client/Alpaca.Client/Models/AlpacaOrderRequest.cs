using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Alpaca.Client.Models;

/// <summary>
/// Order submission body for POST v2/orders. Quantities and prices are strings per
/// Alpaca's API contract.
/// </summary>
[ExcludeFromCodeCoverage]
public class AlpacaOrderRequest
{
    [JsonPropertyName("symbol")]
    public required string Symbol { get; init; }

    [JsonPropertyName("qty")]
    public required string Qty { get; init; }

    /// <summary>"buy" or "sell".</summary>
    [JsonPropertyName("side")]
    public required string Side { get; init; }

    /// <summary>"market" or "stop" (limit types unsupported in v1).</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>"day" for entries/exits, "gtc" for the disaster backstop.</summary>
    [JsonPropertyName("time_in_force")]
    public required string TimeInForce { get; init; }

    [JsonPropertyName("stop_price")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string StopPrice { get; init; }

    /// <summary>
    /// Caller-supplied idempotency key (max 48 chars). Alpaca rejects a duplicate id with
    /// 422, so retried submissions cannot double-order.
    /// </summary>
    [JsonPropertyName("client_order_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string ClientOrderId { get; init; }
}
