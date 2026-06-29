using System.Text.Json.Serialization;

namespace MarketViewer.Contracts.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<Timespan>))]
public enum Timespan
{
    minute,
    hour,
    day,
    week,
    month,
    quarter,
    year
}
