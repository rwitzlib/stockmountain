using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Models.Strategy;
using MarketViewer.Contracts.Records;
using MarketViewer.Contracts.Enums.Backtest;
using Optimus.Services;

namespace Optimus.UnitTests.Services;

public class ExitEvaluatorTests
{
    // Wednesday, so DateUtilities' weekend adjustment stays out of the way.
    private static readonly DateTimeOffset OpenedAt = DateTimeOffset.Parse("2026-07-15T10:00:00-04:00");

    private static StrategyDto BuildStrategy(
        float stopLossValue = -5f,
        float takeProfitValue = 10f,
        ExitValueType exitValueType = ExitValueType.percent,
        Timeframe? timedExitTimeframe = null)
    {
        return new StrategyDto
        {
            Id = "strategy-1",
            UserId = "user-1",
            ExitSettings = new StrategyExitSettings
            {
                StopLoss = new Exit { Type = exitValueType, Value = stopLossValue },
                TakeProfit = new Exit { Type = exitValueType, Value = takeProfitValue },
                TimedExit = new TimedExit { Timeframe = timedExitTimeframe ?? new Timeframe(1, Timespan.day) }
            }
        };
    }

    private static TradeRecord BuildTrade(int shares = 10, float entryPrice = 100f, DateTimeOffset? openedAt = null)
    {
        return new TradeRecord
        {
            Ticker = "TEST",
            Shares = shares,
            EntryPrice = entryPrice,
            EntryPosition = shares * entryPrice,
            OpenedAt = (openedAt ?? OpenedAt).ToString("o")
        };
    }

    [Fact]
    public void Evaluate_ReturnsNull_WhenNothingHit()
    {
        var result = ExitEvaluator.Evaluate(BuildStrategy(), BuildTrade(), 101f, OpenedAt.AddMinutes(5));

        Assert.Null(result);
    }

    [Fact]
    public void Evaluate_ReturnsTimedExit_WhenWindowElapsed()
    {
        var strategy = BuildStrategy(timedExitTimeframe: new Timeframe(30, Timespan.minute));

        var result = ExitEvaluator.Evaluate(strategy, BuildTrade(), 101f, OpenedAt.AddMinutes(31));

        Assert.Equal(BacktestExitReason.timedExit, result);
    }

    [Fact]
    public void Evaluate_ReturnsStopLoss_WhenPercentDropExceedsThreshold()
    {
        var result = ExitEvaluator.Evaluate(BuildStrategy(), BuildTrade(), 94f, OpenedAt.AddMinutes(5));

        Assert.Equal(BacktestExitReason.stopLoss, result);
    }

    [Fact]
    public void Evaluate_ReturnsTakeProfit_WhenPercentGainExceedsThreshold()
    {
        var result = ExitEvaluator.Evaluate(BuildStrategy(), BuildTrade(), 111f, OpenedAt.AddMinutes(5));

        Assert.Equal(BacktestExitReason.takeProfit, result);
    }

    [Fact]
    public void Evaluate_ReturnsStopLoss_WhenBothStopAndTakeProfitHit()
    {
        // Zero thresholds are hit by an unchanged price on both sides: the stop must win the tie.
        var strategy = BuildStrategy(stopLossValue: 0f, takeProfitValue: 0f);

        var result = ExitEvaluator.Evaluate(strategy, BuildTrade(), 100f, OpenedAt.AddMinutes(5));

        Assert.Equal(BacktestExitReason.stopLoss, result);
    }

    [Fact]
    public void Evaluate_ReturnsNull_WhenStopLossValueIsPositiveAndPriceUnchanged()
    {
        // Stop loss entered as a positive magnitude (e.g. "2.5%") must not fire on a flat price.
        var strategy = BuildStrategy(stopLossValue: 2.5f);

        var result = ExitEvaluator.Evaluate(strategy, BuildTrade(), 100f, OpenedAt.AddMinutes(5));

        Assert.Null(result);
    }

    [Fact]
    public void Evaluate_ReturnsStopLoss_WhenStopLossValueIsPositiveAndDropExceedsMagnitude()
    {
        var strategy = BuildStrategy(stopLossValue: 5f);

        var result = ExitEvaluator.Evaluate(strategy, BuildTrade(), 94f, OpenedAt.AddMinutes(5));

        Assert.Equal(BacktestExitReason.stopLoss, result);
    }

    [Fact]
    public void Evaluate_ReturnsTakeProfit_WhenTakeProfitValueIsNegativeAndGainExceedsMagnitude()
    {
        var strategy = BuildStrategy(takeProfitValue: -10f);

        var result = ExitEvaluator.Evaluate(strategy, BuildTrade(), 111f, OpenedAt.AddMinutes(5));

        Assert.Equal(BacktestExitReason.takeProfit, result);
    }

    [Fact]
    public void Evaluate_ReturnsStopLoss_ForFlatValueType()
    {
        // Flat thresholds compare position value change in dollars: 10 shares dropping $10 each = -$100.
        var strategy = BuildStrategy(stopLossValue: -100f, takeProfitValue: 200f, exitValueType: ExitValueType.flat);

        var result = ExitEvaluator.Evaluate(strategy, BuildTrade(), 90f, OpenedAt.AddMinutes(5));

        Assert.Equal(BacktestExitReason.stopLoss, result);
    }

    [Fact]
    public void Evaluate_SkipsPriceBasedExits_WhenPriceUnavailable()
    {
        var result = ExitEvaluator.Evaluate(BuildStrategy(), BuildTrade(), null, OpenedAt.AddMinutes(5));

        Assert.Null(result);
    }

    [Fact]
    public void Evaluate_ReturnsTimedExit_WhenPriceUnavailableButWindowElapsed()
    {
        var strategy = BuildStrategy(timedExitTimeframe: new Timeframe(30, Timespan.minute));

        var result = ExitEvaluator.Evaluate(strategy, BuildTrade(), null, OpenedAt.AddMinutes(31));

        Assert.Equal(BacktestExitReason.timedExit, result);
    }

    // Session bounds for the same Wednesday the trades above open on.
    private static readonly DateTimeOffset SessionClose = DateTimeOffset.Parse("2026-07-15T16:00:00-04:00");
    private static readonly DateTimeOffset NextSessionOpen = DateTimeOffset.Parse("2026-07-16T09:30:00-04:00");

    [Fact]
    public void Evaluate_PullsTimedExitForwardToFinalMinute_WhenProjectedExitLandsAfterClose()
    {
        // Opened 15:55 with a 5m hold: projected exit 16:00 lands exactly at the close,
        // where the worker never runs. Must fire on the 15:59 bar like the backtester.
        var strategy = BuildStrategy(timedExitTimeframe: new Timeframe(5, Timespan.minute));
        var trade = BuildTrade(openedAt: DateTimeOffset.Parse("2026-07-15T15:55:00-04:00"));

        var result = ExitEvaluator.Evaluate(strategy, trade, 101f,
            DateTimeOffset.Parse("2026-07-15T15:59:00-04:00"), (SessionClose, NextSessionOpen));

        Assert.Equal(BacktestExitReason.timedExit, result);
    }

    [Fact]
    public void Evaluate_DoesNotFirePulledForwardExit_BeforeFinalMinute()
    {
        var strategy = BuildStrategy(timedExitTimeframe: new Timeframe(5, Timespan.minute));
        var trade = BuildTrade(openedAt: DateTimeOffset.Parse("2026-07-15T15:55:00-04:00"));

        var result = ExitEvaluator.Evaluate(strategy, trade, 101f,
            DateTimeOffset.Parse("2026-07-15T15:58:50-04:00"), (SessionClose, NextSessionOpen));

        Assert.Null(result);
    }

    [Fact]
    public void Evaluate_LeavesTimedExitAlone_WhenProjectedExitLandsInNextSession()
    {
        // A 1-day hold opened at 10:00 exits tomorrow at 10:00 — after the next open, so
        // it is a deliberate overnight hold and must not be flattened at today's close.
        var strategy = BuildStrategy(timedExitTimeframe: new Timeframe(1, Timespan.day));

        var result = ExitEvaluator.Evaluate(strategy, BuildTrade(), 101f,
            DateTimeOffset.Parse("2026-07-15T15:59:30-04:00"), (SessionClose, NextSessionOpen));

        Assert.Null(result);
    }

    [Fact]
    public void Evaluate_WithoutSessionBounds_KeepsRawProjectedExit()
    {
        // No bounds means no clamp: the raw 16:00 exit hasn't arrived at 15:59. This is
        // the backstop path that fires at the next open if the worker missed the close.
        var strategy = BuildStrategy(timedExitTimeframe: new Timeframe(5, Timespan.minute));
        var trade = BuildTrade(openedAt: DateTimeOffset.Parse("2026-07-15T15:55:00-04:00"));

        var result = ExitEvaluator.Evaluate(strategy, trade, 101f,
            DateTimeOffset.Parse("2026-07-15T15:59:00-04:00"));

        Assert.Null(result);
    }
}
