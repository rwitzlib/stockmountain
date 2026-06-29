using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Models.Backtest;

[ExcludeFromCodeCoverage]
public class BacktestEntryResultCollection
{
    public string Ticker { get; set; }
    public DateTimeOffset BoughtAt { get; set; }
    public float StartPrice { get; set; }
    public int Shares { get; set; }
    public float StartPosition { get; set; }
    public BacktestEntryResult Hold { get; set; }
    public BacktestEntryResult High { get; set; }
    public BacktestEntryResult Other { get; set; }
}
