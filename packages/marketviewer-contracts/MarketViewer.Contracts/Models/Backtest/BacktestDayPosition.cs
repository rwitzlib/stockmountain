using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Models.Backtest;

[ExcludeFromCodeCoverage]
public class BacktestDayPosition
{
    public string Ticker { get; set; }
    public float Price { get; set; }
    public int Shares { get; set; }
    public float Position { get; set; }
    public float Profit { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public bool StoppedOut { get; set; }
}
