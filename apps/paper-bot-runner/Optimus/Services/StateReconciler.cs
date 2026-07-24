using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Records;
using MarketViewer.Contracts.Records.Strategy;

namespace Optimus.Services;

/// <summary>
/// Trade-derived state fields rebuilt from the trade records (the source of truth).
/// </summary>
public record ReconciledState(
    decimal CashBalance,
    decimal TotalEntryCost,
    int OpenPositionsCount,
    HashSet<string> OpenTickers);

/// <summary>
/// Pure computation for rebuilding a strategy's state from its trade records:
/// every buy debits cash by the entry cost and every close credits it with the close
/// value (entry + profit), so cash reduces to
/// startingBalance + closed profits − open entry costs.
/// </summary>
public static class StateReconciler
{
    /// <summary>Cash/entry-cost drift below this is float-vs-decimal rounding noise, not a real discrepancy.</summary>
    public const decimal DriftTolerance = 0.01m;

    public static ReconciledState Compute(decimal startingBalance, IEnumerable<TradeRecord> trades)
    {
        decimal closedProfit = 0;
        decimal openEntryCost = 0;
        var openCount = 0;
        var openTickers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var trade in trades)
        {
            if (trade.OrderStatus == TradeStatus.Open)
            {
                openEntryCost += (decimal)trade.EntryPosition;
                openCount++;
                openTickers.Add(trade.Ticker);
            }
            else
            {
                closedProfit += (decimal)trade.Profit;
            }
        }

        return new ReconciledState(
            CashBalance: startingBalance + closedProfit - openEntryCost,
            TotalEntryCost: openEntryCost,
            OpenPositionsCount: openCount,
            OpenTickers: openTickers);
    }

    public static bool HasDrift(StrategyStateRecord state, ReconciledState expected)
    {
        return Math.Abs(state.CashBalance - expected.CashBalance) > DriftTolerance
            || Math.Abs(state.TotalEntryCost - expected.TotalEntryCost) > DriftTolerance
            || state.OpenPositionsCount != expected.OpenPositionsCount
            || !state.OpenTickers.SetEquals(expected.OpenTickers);
    }
}
