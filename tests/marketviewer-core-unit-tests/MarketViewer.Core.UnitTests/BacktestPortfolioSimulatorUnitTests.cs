using FluentAssertions;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Enums.Backtest;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Models.Backtest;
using MarketViewer.Contracts.Models.Strategy;
using MarketViewer.Contracts.Responses.Market.Backtest;
using MarketViewer.Core.Services;
using Xunit;

namespace MarketViewer.Core.UnitTests;

public class BacktestPortfolioSimulatorUnitTests
{
    private static readonly DateTimeOffset StartDate = DateTimeOffset.Parse("2025-05-27T00:00:00-04:00");
    private static readonly DateTimeOffset BoughtAt = DateTimeOffset.Parse("2025-05-27T09:31:00-04:00");

    [Fact]
    public void Simulate_CopiesExitReasonOntoExecutedTrades()
    {
        // Arrange
        var workerResponse = new WorkerResponse
        {
            Date = StartDate,
            Results =
            [
                new BacktestEntryResultCollection
                {
                    Ticker = "TEST",
                    BoughtAt = BoughtAt,
                    StartPrice = 100f,
                    Shares = 10,
                    StartPosition = 1000f,
                    Hold = new BacktestEntryResult
                    {
                        SoldAt = BoughtAt.AddMinutes(14),
                        EndPrice = 105f,
                        EndPosition = 1050f,
                        Profit = 50f,
                        MaxRunup = 80f,
                        MaxDrawdown = -20f,
                        StoppedOut = true,
                        ExitReason = BacktestExitReason.takeProfit
                    },
                    High = new BacktestEntryResult
                    {
                        SoldAt = BoughtAt.AddMinutes(9),
                        EndPrice = 106f,
                        EndPosition = 1060f,
                        Profit = 60f,
                        MaxRunup = 90f,
                        MaxDrawdown = -10f,
                        StoppedOut = false,
                        ExitReason = BacktestExitReason.soldAtHigh
                    }
                }
            ]
        };

        var positionSettings = new StrategyPositionSettings
        {
            StartingBalance = 10000,
            MaxConcurrentPositions = 5,
            Model = new PositionModel
            {
                Type = PositionType.Fixed,
                Size = 1000
            },
            Cooldown = new Timeframe(15, Timespan.minute)
        };

        // Act
        var response = BacktestPortfolioSimulator.Simulate("backtest-id", 0f, StartDate, positionSettings, [workerResponse]);

        // Assert
        var holdTrade = response.Hold.Trades.Should().ContainSingle().Subject;
        holdTrade.ExitReason.Should().Be(BacktestExitReason.takeProfit);
        holdTrade.StoppedOut.Should().BeTrue();
        holdTrade.MaxRunup.Should().Be(80f);
        holdTrade.MaxDrawdown.Should().Be(-20f);

        var highTrade = response.High.Trades.Should().ContainSingle().Subject;
        highTrade.ExitReason.Should().Be(BacktestExitReason.soldAtHigh);
        highTrade.StoppedOut.Should().BeFalse();
        highTrade.MaxRunup.Should().Be(90f);
        highTrade.MaxDrawdown.Should().Be(-10f);
    }

    [Fact]
    public void Simulate_ExitReasonStaysNull_ForResultsWithoutIt()
    {
        // Arrange — mimics a worker response persisted before ExitReason existed
        var workerResponse = new WorkerResponse
        {
            Date = StartDate,
            Results =
            [
                new BacktestEntryResultCollection
                {
                    Ticker = "TEST",
                    BoughtAt = BoughtAt,
                    StartPrice = 100f,
                    Shares = 10,
                    StartPosition = 1000f,
                    Hold = new BacktestEntryResult
                    {
                        SoldAt = BoughtAt.AddMinutes(14),
                        EndPrice = 101f,
                        EndPosition = 1010f,
                        Profit = 10f,
                        StoppedOut = false
                    },
                    High = new BacktestEntryResult
                    {
                        SoldAt = BoughtAt.AddMinutes(9),
                        EndPrice = 102f,
                        EndPosition = 1020f,
                        Profit = 20f,
                        StoppedOut = false
                    }
                }
            ]
        };

        var positionSettings = new StrategyPositionSettings
        {
            StartingBalance = 10000,
            MaxConcurrentPositions = 5,
            Model = new PositionModel
            {
                Type = PositionType.Fixed,
                Size = 1000
            },
            Cooldown = new Timeframe(15, Timespan.minute)
        };

        // Act
        var response = BacktestPortfolioSimulator.Simulate("backtest-id", 0f, StartDate, positionSettings, [workerResponse]);

        // Assert
        response.Hold.Trades.Should().ContainSingle().Which.ExitReason.Should().BeNull();
        response.High.Trades.Should().ContainSingle().Which.ExitReason.Should().BeNull();
        response.Hold.Trades.Single().MaxRunup.Should().BeNull();
        response.Hold.Trades.Single().MaxDrawdown.Should().BeNull();
        response.High.Trades.Single().MaxRunup.Should().BeNull();
        response.High.Trades.Single().MaxDrawdown.Should().BeNull();
    }

    [Fact]
    public void Simulate_SkipsReentry_WhileTickerStillOpen()
    {
        // Cooldown of zero isolates the open-position rule: the 09:40 signal fires while
        // the 09:31 position is still open, so only the 10:05 re-entry is taken.
        var workerResponse = new WorkerResponse
        {
            Date = StartDate,
            Results =
            [
                CreateResult("TEST", BoughtAt, BoughtAt.AddMinutes(29)),
                CreateResult("TEST", BoughtAt.AddMinutes(9), BoughtAt.AddMinutes(40)),
                CreateResult("TEST", BoughtAt.AddMinutes(34), BoughtAt.AddMinutes(60))
            ]
        };

        var response = BacktestPortfolioSimulator.Simulate(
            "backtest-id", 0f, StartDate, CreateSettings(new Timeframe(0, Timespan.minute)), [workerResponse]);

        response.Hold.Trades.Should().HaveCount(2);
        response.Hold.Trades.Select(t => t.BoughtAt).Should().Equal(BoughtAt, BoughtAt.AddMinutes(34));
    }

    [Fact]
    public void Simulate_SkipsReentry_DuringCooldown()
    {
        // First position closes at 09:40; with a 15-minute cooldown the 09:45 signal is
        // blocked and the 09:56 signal (past the 09:55 expiry) is taken.
        var workerResponse = new WorkerResponse
        {
            Date = StartDate,
            Results =
            [
                CreateResult("TEST", BoughtAt, BoughtAt.AddMinutes(9)),
                CreateResult("TEST", BoughtAt.AddMinutes(14), BoughtAt.AddMinutes(30)),
                CreateResult("TEST", BoughtAt.AddMinutes(25), BoughtAt.AddMinutes(45))
            ]
        };

        var response = BacktestPortfolioSimulator.Simulate(
            "backtest-id", 0f, StartDate, CreateSettings(new Timeframe(15, Timespan.minute)), [workerResponse]);

        response.Hold.Trades.Should().HaveCount(2);
        response.Hold.Trades.Select(t => t.BoughtAt).Should().Equal(BoughtAt, BoughtAt.AddMinutes(25));
    }

    [Fact]
    public void Simulate_AllowSimultaneous_TakesOverlappingPositions()
    {
        var workerResponse = new WorkerResponse
        {
            Date = StartDate,
            Results =
            [
                CreateResult("TEST", BoughtAt, BoughtAt.AddMinutes(29)),
                CreateResult("TEST", BoughtAt.AddMinutes(9), BoughtAt.AddMinutes(40))
            ]
        };

        var response = BacktestPortfolioSimulator.Simulate(
            "backtest-id", 0f, StartDate, CreateSettings(new Timeframe(0, Timespan.minute), allowSimultaneous: true), [workerResponse]);

        response.Hold.Trades.Should().HaveCount(2);
    }

    [Fact]
    public void Simulate_CountsConcurrencySkips_ForSameMinuteSignals()
    {
        // Three same-minute signals with a cap of 1: the alphabetically-first ticker wins
        // the slot, the other two count as concurrency skips.
        var workerResponse = new WorkerResponse
        {
            Date = StartDate,
            Results =
            [
                CreateResult("AAA", BoughtAt, BoughtAt.AddMinutes(29)),
                CreateResult("BBB", BoughtAt, BoughtAt.AddMinutes(29)),
                CreateResult("CCC", BoughtAt, BoughtAt.AddMinutes(29))
            ]
        };

        var settings = CreateSettings(new Timeframe(0, Timespan.minute), maxConcurrentPositions: 1);

        var response = BacktestPortfolioSimulator.Simulate(
            "backtest-id", 0f, StartDate, settings, [workerResponse]);

        var day = response.Hold.Equity.First();
        day.TradesTaken.Should().Be(1);
        day.SignalsSeen.Should().Be(3);
        day.SkippedConcurrency.Should().Be(2);
        day.SkippedFunds.Should().Be(0);
    }

    [Fact]
    public void Simulate_CountsFundsSkips_WhenCashExhausted()
    {
        var workerResponse = new WorkerResponse
        {
            Date = StartDate,
            Results =
            [
                CreateResult("AAA", BoughtAt, BoughtAt.AddMinutes(29)),
                CreateResult("BBB", BoughtAt, BoughtAt.AddMinutes(29))
            ]
        };

        var settings = CreateSettings(new Timeframe(0, Timespan.minute), startingBalance: 1500);

        var response = BacktestPortfolioSimulator.Simulate(
            "backtest-id", 0f, StartDate, settings, [workerResponse]);

        var day = response.Hold.Equity.First();
        day.TradesTaken.Should().Be(1);
        day.SignalsSeen.Should().Be(2);
        day.SkippedFunds.Should().Be(1);
        day.SkippedConcurrency.Should().Be(0);
    }

    [Fact]
    public void Simulate_AttributesDualConstraintSkips_ToConcurrency()
    {
        // Cap of 1 and cash for one position: the second signal fails both constraints
        // and must land in SkippedConcurrency, not SkippedFunds.
        var workerResponse = new WorkerResponse
        {
            Date = StartDate,
            Results =
            [
                CreateResult("AAA", BoughtAt, BoughtAt.AddMinutes(29)),
                CreateResult("BBB", BoughtAt, BoughtAt.AddMinutes(29))
            ]
        };

        var settings = CreateSettings(
            new Timeframe(0, Timespan.minute), startingBalance: 1000, maxConcurrentPositions: 1);

        var response = BacktestPortfolioSimulator.Simulate(
            "backtest-id", 0f, StartDate, settings, [workerResponse]);

        var day = response.Hold.Equity.First();
        day.SkippedConcurrency.Should().Be(1);
        day.SkippedFunds.Should().Be(0);
    }

    [Fact]
    public void Simulate_PolicySkips_DoNotCountAsSignalsSeen()
    {
        // Same scenario as Simulate_SkipsReentry_WhileTickerStillOpen: the 09:40 signal is
        // a duplicate-ticker policy skip, so it appears in neither SignalsSeen nor a skip
        // bucket — the invariant seen == taken + skips still holds.
        var workerResponse = new WorkerResponse
        {
            Date = StartDate,
            Results =
            [
                CreateResult("TEST", BoughtAt, BoughtAt.AddMinutes(29)),
                CreateResult("TEST", BoughtAt.AddMinutes(9), BoughtAt.AddMinutes(40)),
                CreateResult("TEST", BoughtAt.AddMinutes(34), BoughtAt.AddMinutes(60))
            ]
        };

        var response = BacktestPortfolioSimulator.Simulate(
            "backtest-id", 0f, StartDate, CreateSettings(new Timeframe(0, Timespan.minute)), [workerResponse]);

        var day = response.Hold.Equity.First();
        day.TradesTaken.Should().Be(2);
        day.SignalsSeen.Should().Be(2);
        day.SkippedFunds.Should().Be(0);
        day.SkippedConcurrency.Should().Be(0);
    }

    [Fact]
    public void Simulate_EquityPoints_SatisfySignalInvariant()
    {
        var workerResponse = new WorkerResponse
        {
            Date = StartDate,
            Results =
            [
                CreateResult("AAA", BoughtAt, BoughtAt.AddMinutes(29)),
                CreateResult("BBB", BoughtAt, BoughtAt.AddMinutes(29)),
                CreateResult("CCC", BoughtAt.AddMinutes(5), BoughtAt.AddMinutes(45)),
                CreateResult("AAA", BoughtAt.AddMinutes(40), BoughtAt.AddMinutes(60))
            ]
        };

        var settings = CreateSettings(
            new Timeframe(0, Timespan.minute), startingBalance: 2500, maxConcurrentPositions: 2);

        var response = BacktestPortfolioSimulator.Simulate(
            "backtest-id", 0f, StartDate, settings, [workerResponse]);

        foreach (var portfolio in new[] { response.Hold, response.High })
        {
            portfolio.Equity.Should().OnlyContain(day =>
                day.SignalsSeen == day.TradesTaken + day.SkippedFunds + day.SkippedConcurrency);
        }
    }

    [Fact]
    public void Simulate_ProfitFactor_IsNull_NotInfinity_WhenNoLosingTrades()
    {
        // Regression: a run with winners and no losers produced float.PositiveInfinity, which
        // System.Text.Json cannot write, so the orchestrator failed the whole backtest while
        // serializing the stats summary ("An error occurred while processing the backtest request:
        // .NET number values such as positive and negative infinity cannot be written as valid JSON").
        var settings = CreateSettings(new Timeframe(15, Timespan.minute));
        var response = new WorkerResponse
        {
            Date = StartDate,
            Results =
            [
                CreateResult("WIN1", BoughtAt, BoughtAt.AddMinutes(5)),
                CreateResult("WIN2", BoughtAt.AddMinutes(1), BoughtAt.AddMinutes(6))
            ]
        };

        var portfolio = BacktestPortfolioSimulator.Simulate("backtest-id", 0f, StartDate, settings, [response]);

        portfolio.Hold.Trades.Should().HaveCount(2);
        portfolio.Hold.Stats.WinRatio.Should().Be(1f);
        portfolio.Hold.Stats.ProfitFactor.Should().BeNull();
        portfolio.High.Stats.ProfitFactor.Should().BeNull();

        // Exactly what the orchestrator does with default options: must not throw.
        var holdJson = System.Text.Json.JsonSerializer.Serialize(portfolio.Hold.Stats);
        holdJson.Should().Contain("\"ProfitFactor\":null");
        System.Text.Json.JsonSerializer.Serialize(new BacktestEntryStatsSummary
        {
            WinRatio = portfolio.Hold.Stats.WinRatio,
            ProfitFactor = portfolio.Hold.Stats.ProfitFactor,
            TotalTradesTaken = portfolio.Hold.Stats.TotalTradesTaken,
            MaxDrawdown = portfolio.Hold.Stats.MaxDrawdown,
            SharpeRatio = portfolio.Hold.Stats.SharpeRatio
        }).Should().Contain("\"ProfitFactor\":null");
    }

    [Fact]
    public void Simulate_ProfitFactor_IsGrossWinOverGrossLoss_WhenBothExist()
    {
        var settings = CreateSettings(new Timeframe(15, Timespan.minute));
        var loser = CreateResult("LOSE", BoughtAt.AddMinutes(1), BoughtAt.AddMinutes(6));
        loser.Hold.EndPrice = 99.5f; loser.Hold.EndPosition = 995f; loser.Hold.Profit = -5f;
        loser.High.EndPrice = 99f; loser.High.EndPosition = 990f; loser.High.Profit = -10f;
        var response = new WorkerResponse
        {
            Date = StartDate,
            Results = [CreateResult("WIN", BoughtAt, BoughtAt.AddMinutes(5)), loser]
        };

        var portfolio = BacktestPortfolioSimulator.Simulate("backtest-id", 0f, StartDate, settings, [response]);

        portfolio.Hold.Stats.ProfitFactor.Should().Be(10f / 5f);   // +10 / |-5|
        portfolio.High.Stats.ProfitFactor.Should().Be(20f / 10f);  // +20 / |-10|
    }

    [Fact]
    public void Simulate_ProfitFactor_IsNull_WhenNoTrades()
    {
        var settings = CreateSettings(new Timeframe(15, Timespan.minute));
        var portfolio = BacktestPortfolioSimulator.Simulate("backtest-id", 0f, StartDate, settings, [new WorkerResponse { Date = StartDate, Results = [] }]);

        portfolio.Hold.Stats.ProfitFactor.Should().BeNull();
        System.Text.Json.JsonSerializer.Serialize(portfolio.Hold.Stats).Should().NotContain("Infinity");
    }

    private static StrategyPositionSettings CreateSettings(
        Timeframe cooldown,
        bool allowSimultaneous = false,
        float startingBalance = 10000,
        int maxConcurrentPositions = 5)
    {
        return new StrategyPositionSettings
        {
            StartingBalance = startingBalance,
            MaxConcurrentPositions = maxConcurrentPositions,
            AllowSimultaneous = allowSimultaneous,
            Model = new PositionModel
            {
                Type = PositionType.Fixed,
                Size = 1000
            },
            Cooldown = cooldown
        };
    }

    private static BacktestEntryResultCollection CreateResult(string ticker, DateTimeOffset boughtAt, DateTimeOffset soldAt)
    {
        return new BacktestEntryResultCollection
        {
            Ticker = ticker,
            BoughtAt = boughtAt,
            StartPrice = 100f,
            Shares = 10,
            StartPosition = 1000f,
            Hold = new BacktestEntryResult
            {
                SoldAt = soldAt,
                EndPrice = 101f,
                EndPosition = 1010f,
                Profit = 10f
            },
            High = new BacktestEntryResult
            {
                SoldAt = soldAt,
                EndPrice = 102f,
                EndPosition = 1020f,
                Profit = 20f
            }
        };
    }
}
