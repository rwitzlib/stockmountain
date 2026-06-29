using System.Text.Json.Serialization;

namespace MarketViewer.Contracts.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OperandType
{
    Fixed,
    PriceAction,
    Study,
    Property,
    Custom
}
