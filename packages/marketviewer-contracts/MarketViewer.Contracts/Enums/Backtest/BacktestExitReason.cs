using System.Text.Json.Serialization;

namespace MarketViewer.Contracts.Enums.Backtest;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BacktestExitReason
{
    /// <summary>Rode the timed-exit window to its final candle.</summary>
    timedExit,

    /// <summary>Profit target filled.</summary>
    takeProfit,

    /// <summary>Stop filled.</summary>
    stopLoss,

    /// <summary>Candles ran out before the window closed (halt/delisting/no data).</summary>
    endOfData,

    /// <summary>The "high" (max potential) portfolio's natural exit at the max-VWAP candle.</summary>
    soldAtHigh
}
