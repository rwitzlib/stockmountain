using System.Text.Json.Serialization;

namespace MarketViewer.Contracts.Enums.Strategy;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IntegrationType
{
    Default,
    Schwab,
    Fidelity,
    ETrade
}
