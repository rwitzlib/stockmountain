using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Models.Backtest;

/// <summary>
/// One trading day's portfolio snapshot for equity-curve and drawdown calculations.
/// </summary>
[ExcludeFromCodeCoverage]
public class BacktestEquityPoint
{
    public DateTimeOffset Date { get; set; }
    public float StartCash { get; set; }
    public float EndCash { get; set; }
    public float TotalBalance { get; set; }
    public int OpenPositions { get; set; }
    public int MaxConcurrentPositions { get; set; }
    public float DayProfit { get; set; }
    public int TradesTaken { get; set; }
}
