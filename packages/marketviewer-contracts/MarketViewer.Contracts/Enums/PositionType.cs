using System.Text.Json.Serialization;

namespace MarketViewer.Contracts.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PositionType
{
    Fixed,       // Fixed dollar amount per position
    Percentage,   // Percentage of total balance per position
}
