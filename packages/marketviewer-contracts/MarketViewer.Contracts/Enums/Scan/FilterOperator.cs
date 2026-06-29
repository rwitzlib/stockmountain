using System.Text.Json.Serialization;

namespace MarketViewer.Contracts.Enums.Scan;

[JsonConverter(typeof(JsonStringEnumConverter<FilterOperator>))]
public enum FilterOperator
{
    gt,
    lt,
    eq,
    ge,
    le
}
