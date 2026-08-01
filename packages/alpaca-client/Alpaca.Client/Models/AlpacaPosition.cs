using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Alpaca.Client.Models;

/// <summary>An open position as returned by GET v2/positions.</summary>
[ExcludeFromCodeCoverage]
public class AlpacaPosition
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; }

    [JsonPropertyName("qty")]
    public string Qty { get; set; }

    [JsonPropertyName("avg_entry_price")]
    public string AvgEntryPrice { get; set; }

    [JsonPropertyName("market_value")]
    public string MarketValue { get; set; }

    [JsonPropertyName("current_price")]
    public string CurrentPrice { get; set; }

    [JsonPropertyName("unrealized_pl")]
    public string UnrealizedPl { get; set; }

    [JsonIgnore]
    public int Shares => decimal.TryParse(Qty, NumberStyles.Any, CultureInfo.InvariantCulture, out var qty)
        ? (int)qty
        : 0;
}
