using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Alpaca.Client.Models;

/// <summary>Account summary from GET v2/account.</summary>
[ExcludeFromCodeCoverage]
public class AlpacaAccount
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("account_number")]
    public string AccountNumber { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("cash")]
    public string Cash { get; set; }

    [JsonPropertyName("equity")]
    public string Equity { get; set; }

    [JsonPropertyName("buying_power")]
    public string BuyingPower { get; set; }

    [JsonPropertyName("pattern_day_trader")]
    public bool PatternDayTrader { get; set; }

    [JsonPropertyName("daytrade_count")]
    public int DaytradeCount { get; set; }

    [JsonPropertyName("trading_blocked")]
    public bool TradingBlocked { get; set; }
}
