using System.Diagnostics.CodeAnalysis;

namespace Optimus.Adapter.Config;

[ExcludeFromCodeCoverage]
public class AlpacaAdapterConfig
{
    /// <summary>How long a market order may sit unfilled before it is canceled (halted tickers).</summary>
    public int FillTimeoutSeconds { get; set; } = 30;

    public int FillPollIntervalMs { get; set; } = 1000;

    /// <summary>How long to wait after a cancel for the order to settle into a terminal state.</summary>
    public int CancelSettleTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// The disaster backstop rests at this multiple of the strategy's logical stop distance,
    /// so it only fires when the bot is dead, never in normal operation.
    /// </summary>
    public float BackstopStopMultiplier { get; set; } = 3f;

    /// <summary>
    /// Logical stop distance (percent of entry) assumed for strategies with no stop loss
    /// configured — every broker-side position gets a backstop regardless.
    /// </summary>
    public float FallbackBackstopPercent { get; set; } = 25f;
}
