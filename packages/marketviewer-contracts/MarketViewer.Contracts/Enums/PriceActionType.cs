using System.Text.Json.Serialization;

namespace MarketViewer.Contracts.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PriceActionType
{
    open,
    high,
    low,
    close,
    vwap,
    volume
}
