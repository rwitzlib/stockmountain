using System.Text.Json.Serialization;

namespace MarketViewer.Contracts.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<StudyType>))]
public enum StudyType
{
    vwap,
    macd,
    ema,
    sma,
    rsi,
    rvol,
    mamr,
    sr,
    support_resistance
}