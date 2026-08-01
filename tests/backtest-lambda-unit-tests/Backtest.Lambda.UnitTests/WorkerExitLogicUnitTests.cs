using Backtest.Lambda.Models;
using FluentAssertions;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Enums.Backtest;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Models.Strategy;
using MarketViewer.Contracts.Requests.Market.Backtest;
using Massive.Client.Models;

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
    public void CheckStopLoss_Percent_TriggersRegardlessOfSign(float configuredValue)
    {
        var request = CreateRequest(stopLoss: new Exit
        {
            Type = ExitValueType.percent,
            Value = configuredValue
        });

        var bars = new List<Bar>
        {
            CreateBar(1000, high: 101f, low: 99f, close: 100f),   // 0%, no trigger
            CreateBar(2000, high: 100f, low: 94f, close: 96f),    // -4%, no trigger
            CreateBar(3000, high: 100f, low: 90f, close: 92f)     // -8%, trigger
        };

        var triggered = WorkerFunction.CheckStopLoss(request, Shares, EntryPosition, EntryPrice, bars, out var candle);

        triggered.Should().BeTrue();
        candle.Timestamp.Should().Be(3000);
    }

    [Fact]
    public void CheckStopLoss_Percent_DoesNotTriggerAboveThreshold()
    {
        var request = CreateRequest(stopLoss: new Exit
        {
            Type = ExitValueType.percent,
            Value = 5f
        });

        var bars = new List<Bar>
        {
            CreateBar(1000, high: 101f, low: 97f, close: 100f),   // 0%, no trigger
            CreateBar(2000, high: 102f, low: 98f, close: 101f)
        };

        var triggered = WorkerFunction.CheckStopLoss(request, Shares, EntryPosition, EntryPrice, bars, out var candle);

        triggered.Should().BeFalse();
        candle.Should().BeNull();
    }

    [Fact]
    public void CheckStopLoss_Flat_TriggersOnPositionLoss()
    {
        // $50 flat stop on a $1000 position: close of $94 means a $60 loss.
        var request = CreateRequest(stopLoss: new Exit
        {
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
    public void CheckStopLoss_UsesClosePrice_NotLow()
    {
        var request = CreateRequest(stopLoss: new Exit
        {
            Type = ExitValueType.percent,
            Value = 5f
        });

        // Low dips below -5% but close recovers; stops are close-based like live's
        // snapshot polling, so an intra-bar low must not trigger.
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
    public void CheckTakeProfit_Percent_TriggersAtTarget()
    {
        var request = CreateRequest(takeProfit: new Exit
        {
            Type = ExitValueType.percent,
            Value = 10f
        });

        var bars = new List<Bar>
        {
            CreateBar(1000, high: 105f, low: 99f, close: 104f),   // +4%, no trigger
            CreateBar(2000, high: 112f, low: 103f, close: 111f)   // +11%, trigger
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
                Type = ExitValueType.flat,
                Value = 100f
            },
            stopLoss: new Exit
            {
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
                Type = ExitValueType.flat,
                Value = 100f
            },
            stopLoss: new Exit
            {
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

    #endregion

    #region BuildEntryResult / ExitReason

    private static readonly DateTimeOffset EntryStart = DateTimeOffset.Parse("2025-05-27T10:00:00-04:00");

    [Fact]
    public void BuildEntryResult_NoExitHit_HoldIsTimedExit_HighIsSoldAtHigh()
    {
        var request = CreateRequest();
        var entryEnd = EntryStart.AddHours(1);

        // Last candle lands exactly on the window boundary — a true timed exit.
        var bars = new List<Bar>
        {
            CreateBarAt(EntryStart.AddMinutes(1), 100f),
            CreateBarAt(EntryStart.AddMinutes(30), 101f),
            CreateBarAt(entryEnd, 100.5f)
        };

        var result = WorkerFunction.BuildEntryResult(request, CreateEntry(), bars, entryEnd);

        result.Hold.ExitReason.Should().Be(BacktestExitReason.timedExit);
        result.Hold.StoppedOut.Should().BeFalse();
        result.High.ExitReason.Should().Be(BacktestExitReason.soldAtHigh);
        result.High.StoppedOut.Should().BeFalse();
    }

    [Fact]
    public void BuildEntryResult_NoExitHit_ComputesExcursionsThroughEachOutcomeExit()
    {
        var request = CreateRequest();
        var entryEnd = EntryStart.AddHours(1);
        var bars = new List<Bar>
        {
            CreateBarAt(EntryStart.AddMinutes(1), 100f),
            CreateBar(EntryStart.AddMinutes(10).ToUnixTimeMilliseconds(), high: 110f, low: 90f, close: 109f),
            CreateBar(EntryStart.AddMinutes(20).ToUnixTimeMilliseconds(), high: 120f, low: 80f, close: 100f),
            CreateBarAt(entryEnd, 100f)
        };

        var result = WorkerFunction.BuildEntryResult(request, CreateEntry(), bars, entryEnd);

        // SoldAt is the execution minute after the priced bar.
        result.High.SoldAt.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(bars[1].Timestamp).ToOffset(EntryStart.Offset).AddMinutes(1));
        result.High.MaxRunup.Should().Be(100f);
        result.High.MaxDrawdown.Should().Be(-100f);
        result.Hold.MaxRunup.Should().Be(200f);
        result.Hold.MaxDrawdown.Should().Be(-200f);
    }

    [Fact]
    public void BuildEntryResult_TakeProfitHit_BothOutcomesTakeProfit()
    {
        var request = CreateRequest(takeProfit: new Exit
        {
            Type = ExitValueType.percent,
            Value = 5f
        });
        var entryEnd = EntryStart.AddHours(1);

        var bars = new List<Bar>
        {
            CreateBarAt(EntryStart.AddMinutes(1), 100f),
            CreateBar(EntryStart.AddMinutes(10).ToUnixTimeMilliseconds(), high: 110f, low: 104f, close: 106f),
            CreateBarAt(entryEnd, 100.5f)
        };

        var result = WorkerFunction.BuildEntryResult(request, CreateEntry(), bars, entryEnd);

        result.Hold.ExitReason.Should().Be(BacktestExitReason.takeProfit);
        result.Hold.StoppedOut.Should().BeTrue();
        result.High.ExitReason.Should().Be(BacktestExitReason.takeProfit);
        result.High.StoppedOut.Should().BeTrue();
    }

    [Fact]
    public void BuildEntryResult_TakeProfitHit_TruncatesExcursionsAtExitCandle()
    {
        var request = CreateRequest(takeProfit: new Exit
        {
            Type = ExitValueType.percent,
            Value = 5f
        });
        var entryEnd = EntryStart.AddHours(1);
        var bars = new List<Bar>
        {
            CreateBarAt(EntryStart.AddMinutes(1), 100f),
            CreateBar(EntryStart.AddMinutes(10).ToUnixTimeMilliseconds(), high: 110f, low: 96f, close: 106f),
            CreateBar(EntryStart.AddMinutes(20).ToUnixTimeMilliseconds(), high: 150f, low: 50f, close: 100f),
            CreateBarAt(entryEnd, 100f)
        };

        var result = WorkerFunction.BuildEntryResult(request, CreateEntry(), bars, entryEnd);

        result.Hold.MaxRunup.Should().Be(100f);
        result.Hold.MaxDrawdown.Should().Be(-40f);
        result.High.MaxRunup.Should().Be(100f);
        result.High.MaxDrawdown.Should().Be(-40f);
    }

    [Fact]
    public void BuildEntryResult_StopLossHitBeforeTakeProfit_BothOutcomesStopLoss()
    {
        var request = CreateRequest(
            takeProfit: new Exit
            {
                Type = ExitValueType.percent,
                Value = 5f
            },
            stopLoss: new Exit
            {
                Type = ExitValueType.percent,
                Value = 5f
            });
        var entryEnd = EntryStart.AddHours(1);

        var bars = new List<Bar>
        {
            CreateBarAt(EntryStart.AddMinutes(1), 100f),
            CreateBar(EntryStart.AddMinutes(10).ToUnixTimeMilliseconds(), high: 100f, low: 90f, close: 92f),  // stop first
            CreateBar(EntryStart.AddMinutes(20).ToUnixTimeMilliseconds(), high: 120f, low: 95f, close: 115f), // target later
            CreateBarAt(entryEnd, 100.5f)
        };

        var result = WorkerFunction.BuildEntryResult(request, CreateEntry(), bars, entryEnd);

        result.Hold.ExitReason.Should().Be(BacktestExitReason.stopLoss);
        result.High.ExitReason.Should().Be(BacktestExitReason.stopLoss);
    }

    [Fact]
    public void BuildEntryResult_StopLossHit_TruncatesExcursionsAtExitCandle()
    {
        var request = CreateRequest(stopLoss: new Exit
        {
            Type = ExitValueType.percent,
            Value = 5f
        });
        var entryEnd = EntryStart.AddHours(1);
        var bars = new List<Bar>
        {
            CreateBarAt(EntryStart.AddMinutes(1), 100f),
            CreateBar(EntryStart.AddMinutes(10).ToUnixTimeMilliseconds(), high: 104f, low: 90f, close: 95f),
            CreateBar(EntryStart.AddMinutes(20).ToUnixTimeMilliseconds(), high: 150f, low: 50f, close: 100f),
            CreateBarAt(entryEnd, 100f)
        };

        var result = WorkerFunction.BuildEntryResult(request, CreateEntry(), bars, entryEnd);

        result.Hold.MaxRunup.Should().Be(40f);
        result.Hold.MaxDrawdown.Should().Be(-100f);
        result.High.MaxRunup.Should().Be(40f);
        result.High.MaxDrawdown.Should().Be(-100f);
    }

    [Fact]
    public void BuildEntryResult_ConfiguredFillProfit_ClampsRunupToRealizedProfit()
    {
        var request = CreateRequest(takeProfit: new Exit
        {
            Type = ExitValueType.flat,
            Value = 50f
        });
        var entryEnd = EntryStart.AddHours(1);
        var bars = new List<Bar>
        {
            CreateBarAt(EntryStart.AddMinutes(1), 100f),
            // Deliberately inconsistent synthetic OHLC isolates the configured-fill clamp.
            CreateBar(EntryStart.AddMinutes(10).ToUnixTimeMilliseconds(), high: 104f, low: 100f, close: 106f),
            CreateBarAt(entryEnd, 100f)
        };

        var result = WorkerFunction.BuildEntryResult(request, CreateEntry(), bars, entryEnd);

        result.Hold.Profit.Should().Be(50f);
        result.Hold.MaxRunup.Should().Be(50f);
        result.High.MaxRunup.Should().Be(50f);
    }

    [Fact]
    public void BuildEntryResult_TakeProfitBeforeStopLoss_KeepsTakeProfit()
    {
        // With close-based triggers a same-bar tie is impossible (one close cannot be
        // both above the target and below the stop), so ordering decides.
        var request = CreateRequest(
            takeProfit: new Exit
            {
                Type = ExitValueType.percent,
                Value = 5f
            },
            stopLoss: new Exit
            {
                Type = ExitValueType.percent,
                Value = 5f
            });
        var entryEnd = EntryStart.AddHours(1);

        var bars = new List<Bar>
        {
            CreateBarAt(EntryStart.AddMinutes(1), 100f),
            CreateBar(EntryStart.AddMinutes(10).ToUnixTimeMilliseconds(), high: 110f, low: 100f, close: 106f), // target first
            CreateBar(EntryStart.AddMinutes(20).ToUnixTimeMilliseconds(), high: 100f, low: 88f, close: 92f),   // stop later
            CreateBarAt(entryEnd, 100.5f)
        };

        var result = WorkerFunction.BuildEntryResult(request, CreateEntry(), bars, entryEnd);

        result.Hold.ExitReason.Should().Be(BacktestExitReason.takeProfit);
        result.High.ExitReason.Should().Be(BacktestExitReason.takeProfit);
        result.Hold.SoldAt.Should().Be(EntryStart.AddMinutes(11));
    }

    [Fact]
    public void BuildEntryResult_CandlesEndBeforeWindow_HoldIsEndOfData()
    {
        var request = CreateRequest();
        var entryEnd = EntryStart.AddDays(1);

        // Series stops 30 minutes in, far short of the one-day window.
        var bars = new List<Bar>
        {
            CreateBarAt(EntryStart.AddMinutes(1), 100f),
            CreateBarAt(EntryStart.AddMinutes(15), 101f),
            CreateBarAt(EntryStart.AddMinutes(30), 100.5f)
        };

        var result = WorkerFunction.BuildEntryResult(request, CreateEntry(), bars, entryEnd);

        result.Hold.ExitReason.Should().Be(BacktestExitReason.endOfData);
        result.High.ExitReason.Should().Be(BacktestExitReason.soldAtHigh);
    }

    [Fact]
    public void BuildEntryResult_FillsAtSignalBarClose_MatchingLiveSnapshotPrice()
    {
        var request = CreateRequest();
        var entryEnd = EntryStart.AddHours(1);

        var entry = CreateEntry();
        entry.Bars =
        [
            CreateBar(EntryStart.AddMinutes(-1).ToUnixTimeMilliseconds(), high: 49f, low: 47f, close: 48f),
            CreateBar(EntryStart.ToUnixTimeMilliseconds(), high: 51f, low: 49f, close: 50f)
        ];

        var bars = new List<Bar>
        {
            CreateBarAt(EntryStart.AddMinutes(1), 100f),
            CreateBarAt(entryEnd, 110f)
        };

        var result = WorkerFunction.BuildEntryResult(request, entry, bars, entryEnd);

        result.StartPrice.Should().Be(50f);
        result.Shares.Should().Be(20);
        result.BoughtAt.Should().Be(EntryStart.AddMinutes(1));
        result.Hold.EndPrice.Should().Be(110f);
        result.Hold.Profit.Should().Be(110f * 20 - 50f * 20);
    }

    [Fact]
    public void BuildEntryResult_NoSignalBar_FallsBackToFillBarClose()
    {
        var request = CreateRequest();
        var entryEnd = EntryStart.AddHours(1);

        var bars = new List<Bar>
        {
            CreateBar(EntryStart.AddMinutes(1).ToUnixTimeMilliseconds(), high: 105f, low: 95f, close: 100f),
            CreateBarAt(entryEnd, 101f)
        };

        var result = WorkerFunction.BuildEntryResult(request, CreateEntry(), bars, entryEnd);

        result.StartPrice.Should().Be(100f);
        result.Shares.Should().Be(10);
    }

    #endregion

    #region Helpers

    private static StrategyEntry CreateEntry()
    {
        return new StrategyEntry
        {
            Ticker = "TEST",
            Start = EntryStart
        };
    }

    private static Bar CreateBarAt(DateTimeOffset time, float price)
    {
        return CreateBar(time.ToUnixTimeMilliseconds(), high: price, low: price, close: price);
    }

    private static WorkerRequest CreateRequest(Exit stopLoss = null, Exit takeProfit = null)
    {
        return new WorkerRequest
        {
            Date = DateTimeOffset.Parse("2025-05-27"),
            PositionSettings = new StrategyPositionSettings
            {
                StartingBalance = 10000,
                MaxConcurrentPositions = 10,
                Model = new PositionModel
                {
                    Type = PositionType.Fixed,
                    Size = 1000
                },
                Cooldown = new Timeframe(15, Timespan.minute)
            },
            ExitSettings = new StrategyExitSettings
            {
                StopLoss = stopLoss ?? new Exit
                {
                    Type = ExitValueType.percent,
                    Value = 50f
                },
                TakeProfit = takeProfit ?? new Exit
                {
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
