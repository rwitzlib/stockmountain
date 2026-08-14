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

        // 5% stop at a $100 entry puts the stop price at $95.
        var bars = new List<Bar>
        {
            CreateBar(1000, high: 101f, low: 99f, close: 100f),   // low 99, no trigger
            CreateBar(2000, high: 100f, low: 96f, close: 97f),    // low 96, no trigger
            CreateBar(3000, high: 100f, low: 90f, close: 92f, open: 97f)   // low 90, trigger
        };

        var triggered = WorkerFunction.CheckStopLoss(request, Shares, EntryPosition, EntryPrice, bars, out var candle, out var fillPrice);

        triggered.Should().BeTrue();
        candle.Timestamp.Should().Be(3000);
        fillPrice.Should().Be(95f);
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
            CreateBar(1000, high: 101f, low: 97f, close: 100f),   // low above $95 stop
            CreateBar(2000, high: 102f, low: 98f, close: 101f)
        };

        var triggered = WorkerFunction.CheckStopLoss(request, Shares, EntryPosition, EntryPrice, bars, out var candle, out _);

        triggered.Should().BeFalse();
        candle.Should().BeNull();
    }

    [Fact]
    public void CheckStopLoss_Flat_TriggersOnPositionLoss()
    {
        // $50 flat stop on a $1000 position of 10 shares: stop price is $95.
        var request = CreateRequest(stopLoss: new Exit
        {
            Type = ExitValueType.flat,
            Value = 50f
        });

        var bars = new List<Bar>
        {
            CreateBar(1000, high: 101f, low: 96f, close: 98f),               // low 96, no trigger
            CreateBar(2000, high: 99f, low: 93f, close: 94f, open: 98f)      // low 93, trigger
        };

        var triggered = WorkerFunction.CheckStopLoss(request, Shares, EntryPosition, EntryPrice, bars, out var candle, out var fillPrice);

        triggered.Should().BeTrue();
        candle.Timestamp.Should().Be(2000);
        fillPrice.Should().Be(95f);
    }

    [Fact]
    public void CheckStopLoss_TriggersOnIntrabarLow_FillsAtStopPrice()
    {
        var request = CreateRequest(stopLoss: new Exit
        {
            Type = ExitValueType.percent,
            Value = 5f
        });

        // The low wicks through the stop and the close recovers; live paper evaluates
        // the forming websocket bar, so the wick fires the stop at the stop price.
        var bars = new List<Bar>
        {
            CreateBar(1000, high: 101f, low: 92f, close: 99f)
        };

        var triggered = WorkerFunction.CheckStopLoss(request, Shares, EntryPosition, EntryPrice, bars, out _, out var fillPrice);

        triggered.Should().BeTrue();
        fillPrice.Should().Be(95f);
    }

    [Fact]
    public void CheckStopLoss_GapThroughOpen_FillsAtOpen()
    {
        var request = CreateRequest(stopLoss: new Exit
        {
            Type = ExitValueType.percent,
            Value = 5f
        });

        // The bar opens below the $95 stop, so the fill gaps to the open — the loss
        // exceeds the configured stop, as it would live.
        var bars = new List<Bar>
        {
            CreateBar(1000, high: 93f, low: 88f, close: 90f, open: 92f)
        };

        var triggered = WorkerFunction.CheckStopLoss(request, Shares, EntryPosition, EntryPrice, bars, out _, out var fillPrice);

        triggered.Should().BeTrue();
        fillPrice.Should().Be(92f);
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

        // 10% target at a $100 entry puts the target price at $110.
        var bars = new List<Bar>
        {
            CreateBar(1000, high: 105f, low: 99f, close: 104f),               // high 105, no trigger
            CreateBar(2000, high: 112f, low: 103f, close: 111f, open: 104f)   // high 112, trigger
        };

        var triggered = WorkerFunction.CheckTakeProfit(request, Shares, EntryPosition, EntryPrice, bars, out var candle, out var fillPrice);

        triggered.Should().BeTrue();
        candle.Timestamp.Should().Be(2000);
        fillPrice.Should().Be(110f);
    }

    [Fact]
    public void CheckTakeProfit_Flat_UsesTakeProfitValue_NotStopLossValue()
    {
        // Regression: the flat branch previously compared against StopLoss.Value.
        // With a $1 stop-loss configured and a $100 take-profit ($110 target price),
        // a high of $106 must NOT trigger.
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
            CreateBar(1000, high: 106f, low: 100f, close: 105f)
        };

        var triggered = WorkerFunction.CheckTakeProfit(request, Shares, EntryPosition, EntryPrice, bars, out _, out _);

        triggered.Should().BeFalse();
    }

    [Fact]
    public void CheckTakeProfit_Flat_TriggersAtTakeProfitValue()
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
            CreateBar(1000, high: 106f, low: 100f, close: 105f),              // high below $110 target
            CreateBar(2000, high: 112f, low: 105f, close: 111f, open: 106f)   // high 112, trigger
        };

        var triggered = WorkerFunction.CheckTakeProfit(request, Shares, EntryPosition, EntryPrice, bars, out var candle, out var fillPrice);

        triggered.Should().BeTrue();
        candle.Timestamp.Should().Be(2000);
        fillPrice.Should().Be(110f);
    }

    [Fact]
    public void CheckTakeProfit_GapThroughOpen_FillsAtOpen()
    {
        var request = CreateRequest(takeProfit: new Exit
        {
            Type = ExitValueType.percent,
            Value = 10f
        });

        // The bar opens above the $110 target, so the fill gaps up to the open.
        var bars = new List<Bar>
        {
            CreateBar(1000, high: 118f, low: 111f, close: 114f, open: 113f)
        };

        var triggered = WorkerFunction.CheckTakeProfit(request, Shares, EntryPosition, EntryPrice, bars, out _, out var fillPrice);

        triggered.Should().BeTrue();
        fillPrice.Should().Be(113f);
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
    public void BuildEntryResult_TakeProfitFillsAtTargetPrice_BooksConfiguredProfit()
    {
        // $50 flat target on a $1000 position of 10 shares: target price $105. The bar
        // trades through it without gapping, so the fill books exactly the configured value.
        var request = CreateRequest(takeProfit: new Exit
        {
            Type = ExitValueType.flat,
            Value = 50f
        });
        var entryEnd = EntryStart.AddHours(1);
        var bars = new List<Bar>
        {
            CreateBarAt(EntryStart.AddMinutes(1), 100f),
            CreateBar(EntryStart.AddMinutes(10).ToUnixTimeMilliseconds(), high: 106f, low: 100f, close: 104f, open: 101f),
            CreateBarAt(entryEnd, 100f)
        };

        var result = WorkerFunction.BuildEntryResult(request, CreateEntry(), bars, entryEnd);

        result.Hold.Profit.Should().Be(50f);
        result.Hold.EndPrice.Should().Be(105f);
        result.Hold.MaxRunup.Should().Be(60f);
    }

    [Fact]
    public void BuildEntryResult_StopLossGapThrough_BooksLossBeyondConfiguredValue()
    {
        // 5% stop ($95), but the bar opens at $91 — the fill gaps to the open and the
        // realized loss exceeds the configured stop, as it would live.
        var request = CreateRequest(stopLoss: new Exit
        {
            Type = ExitValueType.percent,
            Value = 5f
        });
        var entryEnd = EntryStart.AddHours(1);
        var bars = new List<Bar>
        {
            CreateBarAt(EntryStart.AddMinutes(1), 100f),
            CreateBar(EntryStart.AddMinutes(10).ToUnixTimeMilliseconds(), high: 92f, low: 89f, close: 90f, open: 91f),
            CreateBarAt(entryEnd, 100f)
        };

        var result = WorkerFunction.BuildEntryResult(request, CreateEntry(), bars, entryEnd);

        result.Hold.ExitReason.Should().Be(BacktestExitReason.stopLoss);
        result.Hold.Profit.Should().Be(-90f);
        result.Hold.EndPrice.Should().Be(91f);
    }

    [Fact]
    public void BuildEntryResult_StopAndTargetOnSameBar_StopWins()
    {
        // A wide bar can sweep both extremes; assume the worst case ordering.
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
            CreateBar(EntryStart.AddMinutes(10).ToUnixTimeMilliseconds(), high: 110f, low: 90f, close: 100f, open: 100f),
            CreateBarAt(entryEnd, 100.5f)
        };

        var result = WorkerFunction.BuildEntryResult(request, CreateEntry(), bars, entryEnd);

        result.Hold.ExitReason.Should().Be(BacktestExitReason.stopLoss);
        result.Hold.Profit.Should().Be(-50f);
        result.High.ExitReason.Should().Be(BacktestExitReason.stopLoss);
    }

    [Fact]
    public void BuildEntryResult_TakeProfitBeforeStopLoss_KeepsTakeProfit()
    {
        // The target fires on an earlier bar than the stop, so ordering decides.
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

    private static Bar CreateBar(long timestamp, float high, float low, float close, float? open = null)
    {
        return new Bar
        {
            Timestamp = timestamp,
            High = high,
            Low = low,
            Close = close,
            Open = open ?? close,
            Volume = 1000,
            TransactionCount = 10,
            Vwap = (close + high + low) / 3f
        };
    }

    #endregion
}
