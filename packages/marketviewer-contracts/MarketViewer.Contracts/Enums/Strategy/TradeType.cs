using System.Text.Json.Serialization;

namespace MarketViewer.Contracts.Enums.Strategy;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TradeType
{
    Live,
    Paper
}