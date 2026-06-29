using System.Text.Json.Serialization;

namespace MarketViewer.Contracts.Enums.Scan;

[JsonConverter(typeof(JsonStringEnumConverter<FilterValueType>))]
public enum FilterValueType
{
    CustomAmount,
    Vwap,
    Macd
}
