using System.Diagnostics.CodeAnalysis;

namespace MarketViewer.Contracts.Models.Backtest;

[ExcludeFromCodeCoverage]
public class BacktestDayResultV3
{
    public DateTimeOffset Date { get; set; }
    public BacktestDayDetails Hold { get; set; }
    public BacktestDayDetails High { get; set; }
    public BacktestDayDetails Conditional { get; set; }
}

[ExcludeFromCodeCoverage]
public class BacktestDayDetails
{
    public float StartCashAvailable { get; set; }
    public float EndCashAvailable { get; set; }
    public float TotalBalance { get; set; }
    public float Profit { get; set; }
    public int OpenPositions { get; set; }
    public int MaxConcurrentPositions { get; set; }
    public int TradesTaken { get; set; }
    public List<BacktestDayPosition> Bought { get; set; }
    public List<BacktestDayPosition> Sold { get; set; }
}
