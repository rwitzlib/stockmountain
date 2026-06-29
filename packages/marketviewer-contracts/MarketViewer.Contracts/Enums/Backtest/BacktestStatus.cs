using System.Text.Json.Serialization;

namespace MarketViewer.Contracts.Enums.Backtest;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BacktestStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}
