using Backtest.Lambda.Models;
using FluentAssertions;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Enums.Backtest;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Models.Backtest;
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

    #region CheckTrailingStop

    [Fact]
    public void CheckTrailingStop_NotConfigured_DoesNotTrigger()
    {
        var request = CreateRequest();
        var bars = new List<Bar> { CreateBar(1000, high: 100f, low: 1f, close: 1f) };

        var triggered = WorkerFunction.CheckTrailingStop(request, Shares, EntryPrice, bars, out var candle, out _, out _);

        triggered.Should().BeFalse();
        candle.Should().BeNull();
    }

    [Fact]
    public void CheckTrailingStop_Percent_TrailsFromHighWaterMark()
    {
        var request = CreateRequest(trailingStop: new TrailingStop { Type = ExitValueType.percent, Value = 5f });

        // The mark climbs to 120 on bar 2, putting the trail at 114. Bar 3 dips to 113.
        var bars = new List<Bar>
        {
            CreateBar(1000, high: 110f, low: 100f, close: 108f),
            CreateBar(2000, high: 120f, low: 108f, close: 118f),
            CreateBar(3000, high: 119f, low: 113f, close: 115f, open: 118f)
        };

        var triggered = WorkerFunction.CheckTrailingStop(request, Shares, EntryPrice, bars, out var candle, out var fillPrice, out _);

        triggered.Should().BeTrue();
        candle.Timestamp.Should().Be(3000);
        fillPrice.Should().BeApproximately(114f, 0.0001f);
    }

    [Fact]
    public void CheckTrailingStop_TriggerBarHigh_DoesNotTightenItsOwnStop()
    {
        var request = CreateRequest(trailingStop: new TrailingStop { Type = ExitValueType.percent, Value = 5f });

        // Bar 2 sets a new high of 130 (trail would be 123.5) and dips to 124 in the same
        // bar. Its own high must not count: the stop it is tested against is 114, from
        // bar 1's high of 120, so 124 does not trigger. Bar 3 then trails off 130.
        var bars = new List<Bar>
        {
            CreateBar(1000, high: 120f, low: 100f, close: 118f),
            CreateBar(2000, high: 130f, low: 124f, close: 128f),
            CreateBar(3000, high: 128f, low: 123f, close: 125f, open: 127f)
        };

        var triggered = WorkerFunction.CheckTrailingStop(request, Shares, EntryPrice, bars, out var candle, out var fillPrice, out _);

        triggered.Should().BeTrue();
        candle.Timestamp.Should().Be(3000);
        fillPrice.Should().BeApproximately(123.5f, 0.0001f);
    }

    [Fact]
    public void CheckTrailingStop_TrailsFromEntry_BeforeAnyNewHigh()
    {
        var request = CreateRequest(trailingStop: new TrailingStop { Type = ExitValueType.percent, Value = 5f });

        // No bar exceeds the entry, so the trail sits at 95 off the entry price.
        var bars = new List<Bar>
        {
            CreateBar(1000, high: 100f, low: 96f, close: 98f),
            CreateBar(2000, high: 99f, low: 94f, close: 96f, open: 98f)
        };

        var triggered = WorkerFunction.CheckTrailingStop(request, Shares, EntryPrice, bars, out var candle, out var fillPrice, out _);

        triggered.Should().BeTrue();
        candle.Timestamp.Should().Be(2000);
        fillPrice.Should().Be(95f);
    }

    [Fact]
    public void CheckTrailingStop_Activation_DoesNotArmUntilGainReached()
    {
        var request = CreateRequest(trailingStop: new TrailingStop
        {
            Type = ExitValueType.percent,
            Value = 2f,
            Activation = 5f
        });

        // The mark reaches 104 (below the +5% arm level) then price falls to 90: unarmed,
        // no trigger. Then 106 arms the trail at 103.88 and bar 4's low of 103 fires it.
        var bars = new List<Bar>
        {
            CreateBar(1000, high: 104f, low: 100f, close: 103f),
            CreateBar(2000, high: 103f, low: 90f, close: 95f),
            CreateBar(3000, high: 106f, low: 95f, close: 105f),
            CreateBar(4000, high: 105f, low: 103f, close: 104f, open: 105f)
        };

        var triggered = WorkerFunction.CheckTrailingStop(request, Shares, EntryPrice, bars, out var candle, out var fillPrice, out _);

        triggered.Should().BeTrue();
        candle.Timestamp.Should().Be(4000);
        fillPrice.Should().BeApproximately(103.88f, 0.0001f);
    }

    [Fact]
    public void CheckTrailingStop_Flat_TrailsPositionDollars()
    {
        // $50 on a 10-share position is a $5/share trail: mark 120 → stop 115.
        var request = CreateRequest(trailingStop: new TrailingStop { Type = ExitValueType.flat, Value = 50f });

        var bars = new List<Bar>
        {
            CreateBar(1000, high: 120f, low: 100f, close: 118f),
            CreateBar(2000, high: 119f, low: 114f, close: 116f, open: 118f)
        };

        var triggered = WorkerFunction.CheckTrailingStop(request, Shares, EntryPrice, bars, out var candle, out var fillPrice, out _);

        triggered.Should().BeTrue();
        candle.Timestamp.Should().Be(2000);
        fillPrice.Should().Be(115f);
    }

    [Fact]
    public void CheckTrailingStop_GapThroughOpen_FillsAtOpen()
    {
        var request = CreateRequest(trailingStop: new TrailingStop { Type = ExitValueType.percent, Value = 5f });

        var bars = new List<Bar>
        {
            CreateBar(1000, high: 120f, low: 100f, close: 118f),
            CreateBar(2000, high: 112f, low: 108f, close: 110f, open: 111f)   // opens below the 114 trail
        };

        var triggered = WorkerFunction.CheckTrailingStop(request, Shares, EntryPrice, bars, out _, out var fillPrice, out _);

        triggered.Should().BeTrue();
        fillPrice.Should().Be(111f);
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
    public void BuildEntryResult_SignalCloseFill_MatchesPaperSnapshotPrice()
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
    public void BuildEntryResult_SignalCloseFillWithoutSignalBar_FallsBackToFillBarClose()
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

    [Fact]
    public void BuildEntryResult_DefaultFill_EntersAtNextBarOpen()
    {
        // The signal bar closes at $50 but that price is gone once the signal is known:
        // the default model buys the next bar's open.
        var request = CreateRequest(fillSettings: new BacktestFillSettings { StopSlippagePercent = 0f });
        var entryEnd = EntryStart.AddHours(1);

        var entry = CreateEntry();
        entry.Bars = [CreateBar(EntryStart.ToUnixTimeMilliseconds(), high: 51f, low: 49f, close: 50f)];

        var bars = new List<Bar>
        {
            CreateBar(EntryStart.AddMinutes(1).ToUnixTimeMilliseconds(), high: 102f, low: 99f, close: 101f, open: 100f),
            CreateBarAt(entryEnd, 110f)
        };

        var result = WorkerFunction.BuildEntryResult(request, entry, bars, entryEnd);

        result.StartPrice.Should().Be(100f);
        result.Shares.Should().Be(10);
        result.BoughtAt.Should().Be(EntryStart.AddMinutes(1));
        result.Hold.Profit.Should().Be(100f);
    }

    [Fact]
    public void BuildEntryResult_NullFillSettings_UsesDefaults()
    {
        var request = CreateRequest(stopLoss: new Exit { Type = ExitValueType.percent, Value = 5f });
        request.FillSettings = null;
        var entryEnd = EntryStart.AddHours(1);

        var bars = new List<Bar>
        {
            CreateBar(EntryStart.AddMinutes(1).ToUnixTimeMilliseconds(), high: 101f, low: 99f, close: 99.5f, open: 100f),
            CreateBar(EntryStart.AddMinutes(10).ToUnixTimeMilliseconds(), high: 97f, low: 94f, close: 96f, open: 97f),
            CreateBarAt(entryEnd, 100f)
        };

        var result = WorkerFunction.BuildEntryResult(request, CreateEntry(), bars, entryEnd);

        // Next-bar-open entry at $100, $95 stop slipped by the default 0.5%.
        result.StartPrice.Should().Be(100f);
        result.Hold.ExitReason.Should().Be(BacktestExitReason.stopLoss);
        result.Hold.EndPrice.Should().BeApproximately(95f * 0.995f, 0.0001f);
    }

    [Fact]
    public void BuildEntryResult_StopOnFillBar_TriggersAfterOpenEntry()
    {
        // Entering at the open exposes the trade to the rest of the fill bar.
        var request = CreateRequest(
            stopLoss: new Exit { Type = ExitValueType.percent, Value = 5f },
            fillSettings: new BacktestFillSettings { StopSlippagePercent = 0f });
        var entryEnd = EntryStart.AddHours(1);

        var bars = new List<Bar>
        {
            CreateBar(EntryStart.AddMinutes(1).ToUnixTimeMilliseconds(), high: 100f, low: 94f, close: 96f, open: 100f),
            CreateBarAt(entryEnd, 100f)
        };

        var result = WorkerFunction.BuildEntryResult(request, CreateEntry(), bars, entryEnd);

        result.Hold.ExitReason.Should().Be(BacktestExitReason.stopLoss);
        result.Hold.EndPrice.Should().Be(95f);
        result.Hold.SoldAt.Should().Be(EntryStart.AddMinutes(2));
    }

    [Fact]
    public void BuildEntryResult_MarketSlippage_AppliesToEntryAndTimedExit()
    {
        var request = CreateRequest(fillSettings: new BacktestFillSettings { SlippagePercent = 1f, StopSlippagePercent = 0f });
        var entryEnd = EntryStart.AddHours(1);

        var bars = new List<Bar>
        {
            CreateBarAt(EntryStart.AddMinutes(1), 100f),
            CreateBarAt(entryEnd, 110f)
        };

        var result = WorkerFunction.BuildEntryResult(request, CreateEntry(), bars, entryEnd);

        result.StartPrice.Should().BeApproximately(101f, 0.0001f);
        result.Shares.Should().Be(9);
        result.Hold.ExitReason.Should().Be(BacktestExitReason.timedExit);
        result.Hold.EndPrice.Should().BeApproximately(108.9f, 0.0001f);
        result.High.EndPrice.Should().BeApproximately(108.9f, 0.0001f);
        result.Hold.Profit.Should().BeApproximately((108.9f - 101f) * 9, 0.01f);
    }

    [Fact]
    public void BuildEntryResult_StopSlippage_AppliesOnTopOfGapThroughFill()
    {
        // 5% stop ($95), bar opens through it at $91, then the fill slips another 1%.
        var request = CreateRequest(
            stopLoss: new Exit { Type = ExitValueType.percent, Value = 5f },
            fillSettings: new BacktestFillSettings { StopSlippagePercent = 1f });
        var entryEnd = EntryStart.AddHours(1);

        var bars = new List<Bar>
        {
            CreateBarAt(EntryStart.AddMinutes(1), 100f),
            CreateBar(EntryStart.AddMinutes(10).ToUnixTimeMilliseconds(), high: 92f, low: 89f, close: 90f, open: 91f),
            CreateBarAt(entryEnd, 100f)
        };

        var result = WorkerFunction.BuildEntryResult(request, CreateEntry(), bars, entryEnd);

        result.Hold.ExitReason.Should().Be(BacktestExitReason.stopLoss);
        result.Hold.EndPrice.Should().BeApproximately(90.09f, 0.0001f);
        result.Hold.Profit.Should().BeApproximately(-99.1f, 0.01f);
        result.Hold.MaxDrawdown.Should().BeLessThanOrEqualTo(result.Hold.Profit);
    }

    [Fact]
    public void BuildEntryResult_TakeProfit_NeverSlips()
    {
        var request = CreateRequest(
            takeProfit: new Exit { Type = ExitValueType.percent, Value = 5f },
            fillSettings: new BacktestFillSettings { SlippagePercent = 0f, StopSlippagePercent = 2f });
        var entryEnd = EntryStart.AddHours(1);

        var bars = new List<Bar>
        {
            CreateBarAt(EntryStart.AddMinutes(1), 100f),
            CreateBar(EntryStart.AddMinutes(10).ToUnixTimeMilliseconds(), high: 106f, low: 100f, close: 104f, open: 101f),
            CreateBarAt(entryEnd, 100f)
        };

        var result = WorkerFunction.BuildEntryResult(request, CreateEntry(), bars, entryEnd);

        result.Hold.ExitReason.Should().Be(BacktestExitReason.takeProfit);
        result.Hold.EndPrice.Should().BeApproximately(105f, 0.0001f);
    }

    [Fact]
    public void BuildEntryResult_TrailingStopHit_BothOutcomesTrailingStop_AndTruncateExcursions()
    {
        var request = CreateRequest(trailingStop: new TrailingStop { Type = ExitValueType.percent, Value = 5f });
        var entryEnd = EntryStart.AddHours(1);

        var bars = new List<Bar>
        {
            CreateBarAt(EntryStart.AddMinutes(1), 100f),
            CreateBar(EntryStart.AddMinutes(10).ToUnixTimeMilliseconds(), high: 120f, low: 100f, close: 118f),
            CreateBar(EntryStart.AddMinutes(20).ToUnixTimeMilliseconds(), high: 119f, low: 113f, close: 115f, open: 118f), // trail at 114
            CreateBar(EntryStart.AddMinutes(30).ToUnixTimeMilliseconds(), high: 150f, low: 50f, close: 100f),
            CreateBarAt(entryEnd, 100f)
        };

        var result = WorkerFunction.BuildEntryResult(request, CreateEntry(), bars, entryEnd);

        result.Hold.ExitReason.Should().Be(BacktestExitReason.trailingStop);
        result.Hold.StoppedOut.Should().BeTrue();
        result.Hold.EndPrice.Should().BeApproximately(114f, 0.0001f);
        result.Hold.Profit.Should().BeApproximately(140f, 0.001f);
        result.Hold.SoldAt.Should().Be(EntryStart.AddMinutes(21));
        result.High.ExitReason.Should().Be(BacktestExitReason.trailingStop);
        result.High.EndPrice.Should().BeApproximately(114f, 0.0001f);

        // The 150/50 bar after the exit never counts.
        result.Hold.MaxRunup.Should().Be(200f);
        result.Hold.MaxDrawdown.Should().Be(0f);
    }

    [Fact]
    public void BuildEntryResult_FixedStopAboveTrail_FixedStopWins()
    {
        // 3% fixed stop (97) sits above a 5% trail from entry (95): the fixed stop fires first.
        var request = CreateRequest(
            stopLoss: new Exit { Type = ExitValueType.percent, Value = 3f },
            trailingStop: new TrailingStop { Type = ExitValueType.percent, Value = 5f });
        var entryEnd = EntryStart.AddHours(1);

        var bars = new List<Bar>
        {
            CreateBarAt(EntryStart.AddMinutes(1), 100f),
            CreateBar(EntryStart.AddMinutes(10).ToUnixTimeMilliseconds(), high: 100f, low: 90f, close: 92f, open: 99f),
            CreateBarAt(entryEnd, 100f)
        };

        var result = WorkerFunction.BuildEntryResult(request, CreateEntry(), bars, entryEnd);

        result.Hold.ExitReason.Should().Be(BacktestExitReason.stopLoss);
        result.Hold.EndPrice.Should().BeApproximately(97f, 0.0001f);
    }

    [Fact]
    public void BuildEntryResult_TrailRatchetsAboveFixedStop_TrailingStopWins()
    {
        // Same config, but the mark climbs to 120 first: the trail (114) is now the
        // higher stop and takes over from the fixed 97 stop.
        var request = CreateRequest(
            stopLoss: new Exit { Type = ExitValueType.percent, Value = 3f },
            trailingStop: new TrailingStop { Type = ExitValueType.percent, Value = 5f });
        var entryEnd = EntryStart.AddHours(1);

        var bars = new List<Bar>
        {
            CreateBarAt(EntryStart.AddMinutes(1), 100f),
            CreateBar(EntryStart.AddMinutes(10).ToUnixTimeMilliseconds(), high: 120f, low: 100f, close: 118f),
            CreateBar(EntryStart.AddMinutes(20).ToUnixTimeMilliseconds(), high: 119f, low: 90f, close: 92f, open: 118f),
            CreateBarAt(entryEnd, 100f)
        };

        var result = WorkerFunction.BuildEntryResult(request, CreateEntry(), bars, entryEnd);

        result.Hold.ExitReason.Should().Be(BacktestExitReason.trailingStop);
        result.Hold.EndPrice.Should().BeApproximately(114f, 0.0001f);
    }

    [Fact]
    public void BuildEntryResult_GapThroughBothStops_AttributesToHigherStopLevel()
    {
        // Fixed 3% stop (97) is above the 5% trail from entry (95). A bar opens at 90,
        // through both, so both fills are the open; the exit still belongs to the fixed
        // stop, the level price crossed first — as ExitEvaluator reports it live.
        var request = CreateRequest(
            stopLoss: new Exit { Type = ExitValueType.percent, Value = 3f },
            trailingStop: new TrailingStop { Type = ExitValueType.percent, Value = 5f });
        var entryEnd = EntryStart.AddHours(1);

        var bars = new List<Bar>
        {
            CreateBarAt(EntryStart.AddMinutes(1), 100f),
            CreateBar(EntryStart.AddMinutes(10).ToUnixTimeMilliseconds(), high: 91f, low: 88f, close: 89f, open: 90f),
            CreateBarAt(entryEnd, 100f)
        };

        var result = WorkerFunction.BuildEntryResult(request, CreateEntry(), bars, entryEnd);

        result.Hold.ExitReason.Should().Be(BacktestExitReason.stopLoss);
        result.Hold.EndPrice.Should().BeApproximately(90f, 0.0001f);
    }

    [Fact]
    public void BuildEntryResult_TrailingStopAndTakeProfitSameBar_TrailingStopWins()
    {
        var request = CreateRequest(
            takeProfit: new Exit { Type = ExitValueType.percent, Value = 25f },
            trailingStop: new TrailingStop { Type = ExitValueType.percent, Value = 5f });
        var entryEnd = EntryStart.AddHours(1);

        // Mark 120 → trail 114. The next bar sweeps both the 125 target and the trail:
        // worst case, the stop fills.
        var bars = new List<Bar>
        {
            CreateBarAt(EntryStart.AddMinutes(1), 100f),
            CreateBar(EntryStart.AddMinutes(10).ToUnixTimeMilliseconds(), high: 120f, low: 100f, close: 118f),
            CreateBar(EntryStart.AddMinutes(20).ToUnixTimeMilliseconds(), high: 126f, low: 110f, close: 115f, open: 118f),
            CreateBarAt(entryEnd, 100f)
        };

        var result = WorkerFunction.BuildEntryResult(request, CreateEntry(), bars, entryEnd);

        result.Hold.ExitReason.Should().Be(BacktestExitReason.trailingStop);
        result.High.ExitReason.Should().Be(BacktestExitReason.trailingStop);
    }

    [Fact]
    public void BuildEntryResult_TrailingStop_AppliesStopSlippage()
    {
        var request = CreateRequest(
            trailingStop: new TrailingStop { Type = ExitValueType.percent, Value = 5f },
            fillSettings: new BacktestFillSettings { SlippagePercent = 0f, StopSlippagePercent = 1f });
        var entryEnd = EntryStart.AddHours(1);

        var bars = new List<Bar>
        {
            CreateBarAt(EntryStart.AddMinutes(1), 100f),
            CreateBar(EntryStart.AddMinutes(10).ToUnixTimeMilliseconds(), high: 120f, low: 100f, close: 118f, open: 100f),
            CreateBar(EntryStart.AddMinutes(20).ToUnixTimeMilliseconds(), high: 119f, low: 113f, close: 115f, open: 118f),
            CreateBarAt(entryEnd, 100f)
        };

        var result = WorkerFunction.BuildEntryResult(request, CreateEntry(), bars, entryEnd);

        result.Hold.ExitReason.Should().Be(BacktestExitReason.trailingStop);
        result.Hold.EndPrice.Should().BeApproximately(114f * 0.99f, 0.0001f);
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

    // Exit-mechanics tests pin the legacy fill model (signal close, no slippage) so their
    // round numbers isolate the rule under test; fill-model tests pass their own settings.
    private static WorkerRequest CreateRequest(Exit stopLoss = null, Exit takeProfit = null, BacktestFillSettings fillSettings = null, TrailingStop trailingStop = null)
    {
        return new WorkerRequest
        {
            FillSettings = fillSettings ?? BacktestFillSettings.Legacy,
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
                TrailingStop = trailingStop,
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
