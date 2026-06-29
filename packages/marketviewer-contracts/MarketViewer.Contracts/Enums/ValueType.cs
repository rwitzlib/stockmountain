using System.Text.Json.Serialization;

namespace MarketViewer.Contracts.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<ExitValueType>))]
public enum ExitValueType
{
    percent,
    flat
}
