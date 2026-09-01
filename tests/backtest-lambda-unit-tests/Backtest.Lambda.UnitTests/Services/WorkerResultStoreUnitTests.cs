using Backtest.Lambda.Services;
using FluentAssertions;
using MarketViewer.Contracts.Enums.Backtest;
using MarketViewer.Contracts.Models.Backtest;
using MarketViewer.Contracts.Responses.Market.Backtest;

namespace Backtest.Lambda.UnitTests.Services;

public class WorkerResultStoreUnitTests
{
    [Fact]
    public void SerializeDeserialize_RoundTripsFullResponse()
    {
        var response = new WorkerResponse
        {
            Date = DateTimeOffset.Parse("2026-08-28T00:00:00-04:00"),
            CreditsUsed = 123.4f,
            Hold = new BacktestEntryStats { WinRatio = 0.5f, AvgWin = 10f, AvgLoss = -5f, BalanceChange = 100f },
            High = new BacktestEntryStats { WinRatio = 0.75f, AvgWin = 20f, AvgLoss = -2f, BalanceChange = 200f },
            Errors = ["AAPL at 09:31: candle data unavailable (NotFound)"],
            Results =
            [
                new BacktestEntryResultCollection
                {
                    Ticker = "AAPL",
                    BoughtAt = DateTimeOffset.Parse("2026-08-28T09:31:00-04:00"),
                    StartPrice = 230.5f,
                    Shares = 4,
                    StartPosition = 922f,
                    Hold = new BacktestEntryResult
                    {
                        SoldAt = DateTimeOffset.Parse("2026-08-28T10:31:00-04:00"),
                        EndPrice = 232.5f,
                        EndPosition = 930f,
                        Profit = 8f,
                        MaxRunup = 12f,
                        MaxDrawdown = -3f,
                        ExitReason = BacktestExitReason.timedExit
                    },
                    High = new BacktestEntryResult
                    {
                        SoldAt = DateTimeOffset.Parse("2026-08-28T10:05:00-04:00"),
                        EndPrice = 233.5f,
                        EndPosition = 934f,
                        Profit = 12f,
                        ExitReason = BacktestExitReason.soldAtHigh
                    }
                }
            ]
        };

        var roundTripped = WorkerResultStore.Deserialize(WorkerResultStore.Serialize(response));

        roundTripped.Should().BeEquivalentTo(response);
    }

    [Fact]
    public void SerializeDeserialize_RoundTripsEmptyDay()
    {
        var response = new WorkerResponse
        {
            Date = DateTimeOffset.Parse("2026-08-28T00:00:00Z"),
            CreditsUsed = 1f,
            Results = []
        };

        var roundTripped = WorkerResultStore.Deserialize(WorkerResultStore.Serialize(response));

        roundTripped.Should().BeEquivalentTo(response);
        roundTripped.Errors.Should().BeNull();
    }

    [Fact]
    public void Deserialize_EmptyContent_ReturnsNull()
    {
        WorkerResultStore.Deserialize(string.Empty).Should().BeNull();
    }

    [Fact]
    public void BuildKey_IsStablePerBacktestAndDay()
    {
        var date = DateTimeOffset.Parse("2026-08-28T00:00:00-04:00");

        var key = WorkerResultStore.BuildKey("backtest-123", date);

        key.Should().Be("workerResults/backtest-123/2026-08-28");
        // A retried day must overwrite the same object rather than accumulate copies.
        WorkerResultStore.BuildKey("backtest-123", date).Should().Be(key);
    }

    [Fact]
    public void BuildKey_MissingBacktestId_StillProducesUsableKey()
    {
        WorkerResultStore.BuildKey(null, DateTimeOffset.Parse("2026-08-28"))
            .Should().Be("workerResults/adhoc/2026-08-28");
    }
}
