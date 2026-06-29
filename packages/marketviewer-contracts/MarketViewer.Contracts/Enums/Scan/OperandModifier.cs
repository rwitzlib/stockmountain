using System.Text.Json.Serialization;

namespace MarketViewer.Contracts.Enums.Scan;

[JsonConverter(typeof(JsonStringEnumConverter<OperandModifier>))]
public enum OperandModifier
{
    Value,
    Slope
}
