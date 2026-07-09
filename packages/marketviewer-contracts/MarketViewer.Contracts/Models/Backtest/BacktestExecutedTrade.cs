using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Models.Backtest;

/// <summary>
/// A trade that cleared capital and max-concurrent constraints during portfolio simulation.
/// </summary>
[ExcludeFromCodeCoverage]
public class BacktestExecutedTrade
{
    public string Ticker { get; set; }
    public DateTimeOffset BoughtAt { get; set; }
    public DateTimeOffset SoldAt { get; set; }
    public float StartPrice { get; set; }
    public float EndPrice { get; set; }
    public int Shares { get; set; }
    public float StartPosition { get; set; }
    public float EndPosition { get; set; }
    public float Profit { get; set; }
    public bool StoppedOut { get; set; }
}
