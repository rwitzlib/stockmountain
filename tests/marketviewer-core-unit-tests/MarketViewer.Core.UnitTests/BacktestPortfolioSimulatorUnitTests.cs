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
}
