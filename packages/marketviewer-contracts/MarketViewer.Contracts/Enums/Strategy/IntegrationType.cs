using System.Text.Json.Serialization;

namespace MarketViewer.Contracts.Enums.Strategy;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IntegrationType
{
    Default,
    Schwab,
    Fidelity,
    ETrade,

    /// <summary>Alpaca paper environment — the pre-live integration dress rehearsal.</summary>
    AlpacaPaper,

    /// <summary>
    /// Alpaca live environment, real money. A separate entry from AlpacaPaper on purpose:
    /// a strategy's tier must be explicit and auditable, and promotion is a visible field change.
    /// </summary>
    AlpacaLive
}
