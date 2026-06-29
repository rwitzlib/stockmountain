using System.Text.Json.Serialization;

namespace MarketViewer.Contracts.Enums.Scan;

[JsonConverter(typeof(JsonStringEnumConverter<FilterTypeModifier>))]
public enum FilterTypeModifier
{
    Value,
    Slope
}
