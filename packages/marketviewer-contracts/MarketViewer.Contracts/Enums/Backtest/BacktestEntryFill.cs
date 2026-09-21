using System.Text.Json.Serialization;

namespace MarketViewer.Contracts.Enums.Backtest;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BacktestEntryFill
{
    /// <summary>
    /// Fill at the open of the bar after the signal bar: the first price that can trade
    /// once the signal bar has completed and the signal is known.
    /// </summary>
    nextBarOpen,

    /// <summary>
    /// Fill at the signal bar's close. Optimistic (that price is gone by the time the
    /// signal is observable) but matches the internal paper engine — parity studies only.
    /// </summary>
    signalClose
}
