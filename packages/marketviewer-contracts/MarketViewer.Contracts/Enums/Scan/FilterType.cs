using System.Text.Json.Serialization;

namespace MarketViewer.Contracts.Enums.Scan;

[JsonConverter(typeof(JsonStringEnumConverter<FilterType>))]
public enum FilterType
{
    Volume,
    Price,
    Vwap,
    Macd,
    Float,
    MarketCap
}
