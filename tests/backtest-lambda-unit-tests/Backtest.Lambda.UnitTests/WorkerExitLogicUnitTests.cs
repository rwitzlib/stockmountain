using FluentAssertions;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Models.Strategy;
using MarketViewer.Contracts.Requests.Market.Backtest;
using Polygon.Client.Models;

namespace Backtest.Lambda.UnitTests;

public class WorkerExitLogicUnitTests
{
    private const float EntryPrice = 100f;
    private const int Shares = 10;
    private const float EntryPosition = EntryPrice * Shares;

    #region CheckStopLoss

    [Theory]
    [InlineData(5f)]
    [InlineData(-5f)]
    public void CheckStopLoss_PercentLow_TriggersRegardlessOfSign(float configuredValue)
    {
        var request = CreateRequest(stopLoss: new Exit
        {
            PriceActionType = PriceActionType.low,
            Type = ExitValueType.percent,
            Value = configuredValue
        });

        var bars = new List<Bar>
        {
            CreateBar(1000, high: 101f, low: 99f, close: 100f),   // -1%, no trigger
            CreateBar(2000, high: 100f, low: 94f, close: 96f),    // -6%, trigger
            CreateBar(3000, high: 100f, low: 90f, close: 92f)
        };

        var triggered = WorkerFunction.CheckStopLoss(request, Shares, EntryPosition, EntryPrice, bars, out var candle);

        triggered.Should().BeTrue();
        candle.Timestamp.Should().Be(2000);
    }

    [Fact]
    public void CheckStopLoss_PercentLow_DoesNotTriggerAboveThreshold()
    {
        var request = CreateRequest(stopLoss: new Exit
        {
            PriceActionType = PriceActionType.low,
            Type = ExitValueType.percent,
            Value = 5f
        });

        var bars = new List<Bar>
        {
            CreateBar(1000, high: 101f, low: 97f, close: 100f),   // -3%, no trigger
            CreateBar(2000, high: 102f, low: 98f, close: 101f)
        };

        var triggered = WorkerFunction.CheckStopLoss(request, Shares, EntryPosition, EntryPrice, bars, out var candle);

        triggered.Should().BeFalse();
        candle.Should().BeNull();
    }

    [Fact]
    public void CheckStopLoss_FlatClose_TriggersOnPositionLoss()
    {
        // $50 flat stop on a $1000 position: close of $94 means a $60 loss.
        var request = CreateRequest(stopLoss: new Exit
        {
            PriceActionType = PriceActionType.close,
            Type = ExitValueType.flat,
            Value = 50f
        });

        var bars = new List<Bar>
        {
            CreateBar(1000, high: 101f, low: 96f, close: 98f),    // -$20, no trigger
            CreateBar(2000, high: 99f, low: 93f, close: 94f)      // -$60, trigger
        };

        var triggered = WorkerFunction.CheckStopLoss(request, Shares, EntryPosition, EntryPrice, bars, out var candle);

        triggered.Should().BeTrue();
        candle.Timestamp.Should().Be(2000);
    }

    [Fact]
    public void CheckStopLoss_UsesClosePrice_NotLow_ForClosePriceAction()
    {
        var request = CreateRequest(stopLoss: new Exit
        {
            PriceActionType = PriceActionType.close,
            Type = ExitValueType.percent,
            Value = 5f
        });

        // Low dips below -5% but close recovers; a close-based stop must not trigger.
        var bars = new List<Bar>
        {
            CreateBar(1000, high: 101f, low: 92f, close: 99f)
        };

        var triggered = WorkerFunction.CheckStopLoss(request, Shares, EntryPosition, EntryPrice, bars, out _);

        triggered.Should().BeFalse();
    }

    #endregion

    #region CheckTakeProfit

    [Fact]
    public void CheckTakeProfit_PercentHigh_TriggersAtTarget()
    {
        var request = CreateRequest(takeProfit: new Exit
        {
            PriceActionType = PriceActionType.high,
            Type = ExitValueType.percent,
            Value = 10f
        });

        var bars = new List<Bar>
        {
            CreateBar(1000, high: 105f, low: 99f, close: 104f),   // +5%, no trigger
            CreateBar(2000, high: 111f, low: 103f, close: 108f)   // +11%, trigger
        };

        var triggered = WorkerFunction.CheckTakeProfit(request, Shares, EntryPosition, EntryPrice, bars, out var candle);

        triggered.Should().BeTrue();
        candle.Timestamp.Should().Be(2000);
    }

    [Fact]
    public void CheckTakeProfit_FlatClose_UsesTakeProfitValue_NotStopLossValue()
    {
        // Regression: the close/flat branch previously compared against StopLoss.Value.
        // With a $1 stop-loss configured and a $100 take-profit, a $50 gain must NOT trigger.
        var request = CreateRequest(
            takeProfit: new Exit
            {
                PriceActionType = PriceActionType.close,
                Type = ExitValueType.flat,
                Value = 100f
            },
            stopLoss: new Exit
            {
                PriceActionType = PriceActionType.low,
                Type = ExitValueType.flat,
                Value = 1f
            });

        var bars = new List<Bar>
        {
            CreateBar(1000, high: 106f, low: 100f, close: 105f)   // +$50 on close
        };

        var triggered = WorkerFunction.CheckTakeProfit(request, Shares, EntryPosition, EntryPrice, bars, out _);

        triggered.Should().BeFalse();
    }

    [Fact]
    public void CheckTakeProfit_FlatClose_TriggersAtTakeProfitValue()
    {
        var request = CreateRequest(
            takeProfit: new Exit
            {
                PriceActionType = PriceActionType.close,
                Type = ExitValueType.flat,
                Value = 100f
            },
            stopLoss: new Exit
            {
                PriceActionType = PriceActionType.low,
                Type = ExitValueType.flat,
                Value = 1f
            });

        var bars = new List<Bar>
        {
            CreateBar(1000, high: 106f, low: 100f, close: 105f),  // +$50, no trigger
            CreateBar(2000, high: 112f, low: 105f, close: 111f)   // +$110, trigger
        };

        var triggered = WorkerFunction.CheckTakeProfit(request, Shares, EntryPosition, EntryPrice, bars, out var candle);

        triggered.Should().BeTrue();
        candle.Timestamp.Should().Be(2000);
    }

    [Fact]
    public void CheckTakeProfit_VwapFlat_UsesTakeProfitValue_NotStopLossValue()
    {
        // Regression: the vwap/flat branch previously compared against StopLoss.Value.
        var request = CreateRequest(
            takeProfit: new Exit
            {
                PriceActionType = PriceActionType.vwap,
                Type = ExitValueType.flat,
                Value = 100f
            },
            stopLoss: new Exit
            {
                PriceActionType = PriceActionType.low,
                Type = ExitValueType.flat,
                Value = 1f
            });

        var bars = new List<Bar>
        {
            CreateBar(1000, high: 106f, low: 103f, close: 105f)   // vwap ≈ 104.67, +$46, no trigger
        };

        var triggered = WorkerFunction.CheckTakeProfit(request, Shares, EntryPosition, EntryPrice, bars, out _);

        triggered.Should().BeFalse();
    }

    #endregion

    #region Helpers

    private static WorkerRequest CreateRequest(Exit stopLoss = null, Exit takeProfit = null)
    {
        return new WorkerRequest
        {
            Date = DateTimeOffset.Parse("2025-05-27"),
            ExitSettings = new StrategyExitSettings
            {
                StopLoss = stopLoss ?? new Exit
                {
                    PriceActionType = PriceActionType.low,
                    Type = ExitValueType.percent,
                    Value = 50f
                },
                TakeProfit = takeProfit ?? new Exit
                {
                    PriceActionType = PriceActionType.high,
                    Type = ExitValueType.percent,
                    Value = 1000f
                },
                TimedExit = new TimedExit
                {
                    Timeframe = new Timeframe(1, Timespan.day)
                }
            }
        };
    }

    private static Bar CreateBar(long timestamp, float high, float low, float close)
    {
        return new Bar
        {
            Timestamp = timestamp,
            High = high,
            Low = low,
            Close = close,
            Open = close,
            Volume = 1000,
            TransactionCount = 10,
            Vwap = (close + high + low) / 3f
        };
    }

    #endregion
}
