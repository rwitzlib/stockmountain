using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Models.Strategy;
using MarketViewer.Contracts.Records;
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

    private static TradeRecord BuildTrade(int shares = 10, float entryPrice = 100f)
    {
        return new TradeRecord
        {
            Ticker = "TEST",
            Shares = shares,
            EntryPrice = entryPrice,
            EntryPosition = shares * entryPrice,
            OpenedAt = OpenedAt.ToString("o")
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

        Assert.Equal(ExitReason.TimedExit, result);
    }

    [Fact]
    public void Evaluate_ReturnsStopLoss_WhenPercentDropExceedsThreshold()
    {
        var result = ExitEvaluator.Evaluate(BuildStrategy(), BuildTrade(), 94f, OpenedAt.AddMinutes(5));

        Assert.Equal(ExitReason.StopLoss, result);
    }

    [Fact]
    public void Evaluate_ReturnsTakeProfit_WhenPercentGainExceedsThreshold()
    {
        var result = ExitEvaluator.Evaluate(BuildStrategy(), BuildTrade(), 111f, OpenedAt.AddMinutes(5));

        Assert.Equal(ExitReason.TakeProfit, result);
    }

    [Fact]
    public void Evaluate_ReturnsStopLoss_WhenBothStopAndTakeProfitHit()
    {
        // Degenerate thresholds where any price satisfies both: the stop must win the tie.
        var strategy = BuildStrategy(stopLossValue: 100f, takeProfitValue: -100f);

        var result = ExitEvaluator.Evaluate(strategy, BuildTrade(), 100f, OpenedAt.AddMinutes(5));

        Assert.Equal(ExitReason.StopLoss, result);
    }

    [Fact]
    public void Evaluate_ReturnsStopLoss_ForFlatValueType()
    {
        // Flat thresholds compare position value change in dollars: 10 shares dropping $10 each = -$100.
        var strategy = BuildStrategy(stopLossValue: -100f, takeProfitValue: 200f, exitValueType: ExitValueType.flat);

        var result = ExitEvaluator.Evaluate(strategy, BuildTrade(), 90f, OpenedAt.AddMinutes(5));

        Assert.Equal(ExitReason.StopLoss, result);
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

        Assert.Equal(ExitReason.TimedExit, result);
    }
}
