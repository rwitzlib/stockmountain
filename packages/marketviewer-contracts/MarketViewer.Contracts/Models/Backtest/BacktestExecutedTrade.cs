using MarketViewer.Contracts.Enums.Backtest;
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

    /// <summary>Deprecated in favor of <see cref="ExitReason"/>; kept for old persisted results and existing consumers.</summary>
    public bool StoppedOut { get; set; }

    /// <summary>Why the position exited. Null on results persisted before this field existed.</summary>
    public BacktestExitReason? ExitReason { get; set; }
}
