using System.Text.Json.Serialization;

namespace MarketViewer.Contracts.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<MarketDataStatus>))]
public enum MarketDataStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Skipped,
    Missing
}
