using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Records;
using MarketViewer.Contracts.Records.Strategy;
using Optimus.Services;

namespace Optimus.UnitTests.Services;

public class StateReconcilerTests
{
    private static TradeRecord OpenTrade(string ticker, float entryPosition) => new()
    {
        Ticker = ticker,
        OrderStatus = TradeStatus.Open,
        EntryPosition = entryPosition
    };

    private static TradeRecord ClosedTrade(string ticker, float profit) => new()
    {
        Ticker = ticker,
        OrderStatus = TradeStatus.Closed,
        EntryPosition = 1000f,
        Profit = profit
    };

    [Fact]
    public void Compute_NoTrades_ReturnsStartingBalance()
    {
        var result = StateReconciler.Compute(10000m, []);

        Assert.Equal(10000m, result.CashBalance);
        Assert.Equal(0m, result.TotalEntryCost);
        Assert.Equal(0, result.OpenPositionsCount);
        Assert.Empty(result.OpenTickers);
    }

    [Fact]
    public void Compute_ClosedTradesAddProfit_OpenTradesLockEntryCost()
    {
        var trades = new[]
        {
            ClosedTrade("AAA", 150f),
            ClosedTrade("BBB", -50f),
            OpenTrade("CCC", 1000f),
            OpenTrade("DDD", 2000f)
        };

        var result = StateReconciler.Compute(10000m, trades);

        // 10000 + (150 - 50) - (1000 + 2000)
        Assert.Equal(7100m, result.CashBalance);
        Assert.Equal(3000m, result.TotalEntryCost);
        Assert.Equal(2, result.OpenPositionsCount);
        Assert.Equal(new HashSet<string> { "CCC", "DDD" }, result.OpenTickers);
    }

    [Fact]
    public void Compute_SimultaneousPositionsInSameTicker_CountedPerPositionButOneTicker()
    {
        var trades = new[]
        {
            OpenTrade("AAA", 1000f),
            OpenTrade("AAA", 1500f)
        };

        var result = StateReconciler.Compute(10000m, trades);

        Assert.Equal(7500m, result.CashBalance);
        Assert.Equal(2500m, result.TotalEntryCost);
        Assert.Equal(2, result.OpenPositionsCount);
        Assert.Single(result.OpenTickers);
    }

    [Fact]
    public void HasDrift_MatchingState_ReturnsFalse()
    {
        var state = new StrategyStateRecord
        {
            CashBalance = 7100m,
            TotalEntryCost = 3000m,
            OpenPositionsCount = 2,
            OpenTickers = ["CCC", "DDD"]
        };

        var expected = new ReconciledState(7100m, 3000m, 2, ["CCC", "DDD"]);

        Assert.False(StateReconciler.HasDrift(state, expected));
    }

    [Fact]
    public void HasDrift_WithinRoundingTolerance_ReturnsFalse()
    {
        var state = new StrategyStateRecord
        {
            CashBalance = 7100.005m,
            TotalEntryCost = 2999.995m,
            OpenPositionsCount = 2,
            OpenTickers = ["CCC", "DDD"]
        };

        var expected = new ReconciledState(7100m, 3000m, 2, ["CCC", "DDD"]);

        Assert.False(StateReconciler.HasDrift(state, expected));
    }

    [Fact]
    public void HasDrift_DroppedCloseCredit_ReturnsTrue()
    {
        // The lost-close signature: cash missing a close value, ticker stuck in the open set.
        var state = new StrategyStateRecord
        {
            CashBalance = 5000m,
            TotalEntryCost = 2000m,
            OpenPositionsCount = 2,
            OpenTickers = ["CCC", "STUCK"]
        };

        var expected = new ReconciledState(6100m, 1000m, 1, ["CCC"]);

        Assert.True(StateReconciler.HasDrift(state, expected));
    }

    [Fact]
    public void HasDrift_CountMismatchAlone_ReturnsTrue()
    {
        var state = new StrategyStateRecord
        {
            CashBalance = 7100m,
            TotalEntryCost = 3000m,
            OpenPositionsCount = 3,
            OpenTickers = ["CCC", "DDD"]
        };

        var expected = new ReconciledState(7100m, 3000m, 2, ["CCC", "DDD"]);

        Assert.True(StateReconciler.HasDrift(state, expected));
    }
}
