using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Alpaca.Client.Models;

/// <summary>
/// An order as returned by Alpaca's trading API. Numeric fields arrive as strings on the
/// wire; use the typed convenience properties instead of parsing at call sites.
/// </summary>
[ExcludeFromCodeCoverage]
public class AlpacaOrder
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("client_order_id")]
    public string ClientOrderId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; }

    [JsonPropertyName("side")]
    public string Side { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("time_in_force")]
    public string TimeInForce { get; set; }

    [JsonPropertyName("qty")]
    public string Qty { get; set; }

    [JsonPropertyName("filled_qty")]
    public string FilledQty { get; set; }

    [JsonPropertyName("filled_avg_price")]
    public string FilledAvgPrice { get; set; }

    [JsonPropertyName("stop_price")]
    public string StopPrice { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("filled_at")]
    public DateTimeOffset? FilledAt { get; set; }

    [JsonPropertyName("canceled_at")]
    public DateTimeOffset? CanceledAt { get; set; }

    [JsonIgnore]
    public bool IsFilled => Status == OrderStatus.Filled;

    /// <summary>
    /// True once the order can no longer change state (filled, canceled, expired, rejected).
    /// A partially_filled order is NOT terminal.
    /// </summary>
    [JsonIgnore]
    public bool IsTerminal => Status is OrderStatus.Filled or OrderStatus.Canceled
        or OrderStatus.Expired or OrderStatus.Rejected;

    /// <summary>Filled share count; 0 when nothing has filled yet.</summary>
    [JsonIgnore]
    public int FilledShares => decimal.TryParse(FilledQty, NumberStyles.Any, CultureInfo.InvariantCulture, out var qty)
        ? (int)qty
        : 0;

    /// <summary>Average fill price, or null when nothing has filled yet.</summary>
    [JsonIgnore]
    public float? FilledPrice => float.TryParse(FilledAvgPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out var price) && price > 0
        ? price
        : null;
}

/// <summary>Alpaca order status strings this codebase cares about.</summary>
public static class OrderStatus
{
    public const string New = "new";
    public const string PartiallyFilled = "partially_filled";
    public const string Filled = "filled";
    public const string Canceled = "canceled";
    public const string Expired = "expired";
    public const string Rejected = "rejected";
    public const string Accepted = "accepted";
    public const string PendingNew = "pending_new";
    public const string PendingCancel = "pending_cancel";
}
