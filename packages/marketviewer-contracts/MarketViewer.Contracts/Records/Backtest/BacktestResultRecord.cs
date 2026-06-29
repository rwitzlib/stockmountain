using MarketViewer.Contracts.Enums.Backtest;
using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Records.Backtest;

/// <summary>
/// Database record for storing information about backtest results.
/// </summary>
[ExcludeFromCodeCoverage]
public class BacktestResultRecord
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public BacktestStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public float CreditsUsed { get; set; }
    public DateTimeOffset Date { get; set; }
    /// <summary>
    /// Seconds taken to complete the backtest.
    /// </summary>
    public float Duration { get; set; }
    public string Filter { get; set; }
}
