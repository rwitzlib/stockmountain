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
    sr,
    support_resistance
}