using FluentAssertions;
using MarketViewer.Application.Handlers.Market.Backtest;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Enums.Backtest;
using MarketViewer.Contracts.Interfaces;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Models.Backtest;
using MarketViewer.Contracts.Models.Strategy;
using MarketViewer.Contracts.Records.Backtest;
using MarketViewer.Contracts.Requests.Market;
using MarketViewer.Contracts.Requests.Market.Backtest;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Contracts.Responses.Market.Backtest;
using MarketViewer.Core.Auth;
using MarketViewer.Core.Services;
using Massive.Client.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Text.Json;
using Xunit;

namespace MarketViewer.Application.UnitTests.Handlers.Backtest;

public class BacktestShareHandlerUnitTests
{
    private const string OwnerId = "user_owner";
    private const string BacktestId = "backtest-123";
    private const string SecretFilter = "RSI(14) < 30 AND Volume > 1000000";

    private readonly Mock<IBacktestRepository> _repository = new();
    private readonly Mock<IMarketDataRepository> _marketData = new();
    private readonly BacktestShareHandler _classUnderTest;

    public BacktestShareHandlerUnitTests()
    {
        var authContext = new AuthContext { UserId = OwnerId, IsAuthenticated = true };
        _classUnderTest = new BacktestShareHandler(
            authContext,
            _repository.Object,
            _marketData.Object,
            NullLogger<BacktestShareHandler>.Instance);
    }

    #region CreateShare

    [Fact]
    public async Task CreateShare_HappyPath_ReturnsShareIdAndWritesPayload()
    {
        var captured = GivenCompletedBacktestAndCapturePayload();
        GivenBenchmarkBars();

        var result = await _classUnderTest.CreateShare(BacktestId, new BacktestShareCreateRequest());

        result.Status.Should().Be(HttpStatusCode.OK);
        result.Data.ShareId.Should().MatchRegex("^[A-Za-z0-9_-]{20,64}$");
        result.Data.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(30), TimeSpan.FromMinutes(1));

        captured.Payload.Should().NotBeNull();
        captured.ShareId.Should().Be(result.Data.ShareId);
        captured.Payload.SchemaVersion.Should().Be(BacktestSharePayload.CurrentSchemaVersion);
        captured.Payload.Result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateShare_TwoCalls_MintDistinctShareIds()
    {
        GivenCompletedBacktestAndCapturePayload();
        GivenBenchmarkBars();

        var first = await _classUnderTest.CreateShare(BacktestId, new BacktestShareCreateRequest());
        var second = await _classUnderTest.CreateShare(BacktestId, new BacktestShareCreateRequest());

        first.Data.ShareId.Should().NotBe(second.Data.ShareId);
    }

    [Fact]
    public async Task CreateShare_Default_MasksConfigAndLeaksNoValues()
    {
        var captured = GivenCompletedBacktestAndCapturePayload();
        GivenBenchmarkBars();

        var result = await _classUnderTest.CreateShare(BacktestId, new BacktestShareCreateRequest { IncludeConfig = false });

        result.Status.Should().Be(HttpStatusCode.OK);

        var config = captured.Payload.Config;
        config.Masked.Should().BeTrue();
        config.EntryFilterCount.Should().Be(1);
        config.HasStopLoss.Should().BeTrue();
        config.HasProfitTarget.Should().BeTrue();
        config.HasTimedExit.Should().BeTrue();
        config.PositionSettings.Should().BeNull();
        config.ExitSettings.Should().BeNull();
        config.EntrySettings.Should().BeNull();

        // The payload is public: serialize the whole thing and prove the recipe is absent.
        var json = JsonSerializer.Serialize(captured.Payload);
        json.Should().NotContain("RSI(14)");
        json.Should().NotContainAny(OwnerId, BacktestId);
    }

    [Fact]
    public async Task CreateShare_IncludeConfig_CarriesFullSettings()
    {
        var captured = GivenCompletedBacktestAndCapturePayload();
        GivenBenchmarkBars();

        var result = await _classUnderTest.CreateShare(BacktestId, new BacktestShareCreateRequest { IncludeConfig = true });

        result.Status.Should().Be(HttpStatusCode.OK);

        var config = captured.Payload.Config;
        config.Masked.Should().BeFalse();
        config.EntrySettings.Filters.Should().ContainSingle(f => f == SecretFilter);
        config.PositionSettings.Should().NotBeNull();
        config.ExitSettings.Should().NotBeNull();
        config.EntryFilterCount.Should().BeNull();
    }

    [Fact]
    public async Task CreateShare_StripsBacktestIdAndOwnerFromResult()
    {
        var captured = GivenCompletedBacktestAndCapturePayload();
        GivenBenchmarkBars();

        await _classUnderTest.CreateShare(BacktestId, new BacktestShareCreateRequest());

        captured.Payload.Result.Id.Should().BeNull();
        captured.Payload.Result.CreditsUsed.Should().Be(0);
        JsonSerializer.Serialize(captured.Payload).Should().NotContainAny(OwnerId, BacktestId);
    }

    [Fact]
    public async Task CreateShare_BenchmarkFetchFails_StillCreatesShare()
    {
        var captured = GivenCompletedBacktestAndCapturePayload();
        _marketData.Setup(m => m.GetStockDataAsync(It.IsAny<StocksRequest>()))
            .ThrowsAsync(new InvalidOperationException("polygon down"));

        var result = await _classUnderTest.CreateShare(BacktestId, new BacktestShareCreateRequest());

        result.Status.Should().Be(HttpStatusCode.OK);
        captured.Payload.Benchmark.Should().BeNull();
    }

    [Fact]
    public async Task CreateShare_BenchmarkBars_AreSortedAndMapped()
    {
        var captured = GivenCompletedBacktestAndCapturePayload();
        GivenBenchmarkBars();

        await _classUnderTest.CreateShare(BacktestId, new BacktestShareCreateRequest());

        captured.Payload.Benchmark.Should().HaveCount(2);
        captured.Payload.Benchmark[0].Close.Should().Be(600f);
        captured.Payload.Benchmark[1].Close.Should().Be(610f);
        captured.Payload.Benchmark.Should().BeInAscendingOrder(p => p.Date);
    }

    [Fact]
    public async Task CreateShare_NotOwner_Returns404WithoutWriting()
    {
        GivenCompletedBacktestAndCapturePayload(userId: "someone_else");

        var result = await _classUnderTest.CreateShare(BacktestId, new BacktestShareCreateRequest());

        result.Status.Should().Be(HttpStatusCode.NotFound);
        _repository.Verify(r => r.PutShare(It.IsAny<string>(), It.IsAny<BacktestSharePayload>()), Times.Never);
    }

    [Fact]
    public async Task CreateShare_MissingRecord_Returns404()
    {
        _repository.Setup(r => r.Get(BacktestId)).ReturnsAsync((BacktestContextRecord)null);

        var result = await _classUnderTest.CreateShare(BacktestId, new BacktestShareCreateRequest());

        result.Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateShare_IncompleteBacktest_ReturnsBadRequest()
    {
        GivenCompletedBacktestAndCapturePayload(status: BacktestStatus.InProgress);

        var result = await _classUnderTest.CreateShare(BacktestId, new BacktestShareCreateRequest());

        result.Status.Should().Be(HttpStatusCode.BadRequest);
        _repository.Verify(r => r.PutShare(It.IsAny<string>(), It.IsAny<BacktestSharePayload>()), Times.Never);
    }

    [Fact]
    public async Task CreateShare_TitleIsTrimmedAndCapped()
    {
        var captured = GivenCompletedBacktestAndCapturePayload();
        GivenBenchmarkBars();

        await _classUnderTest.CreateShare(BacktestId, new BacktestShareCreateRequest
        {
            Title = "  " + new string('x', 200) + "  "
        });

        captured.Payload.Title.Should().HaveLength(100);
    }

    #endregion

    #region GetShareJson

    [Fact]
    public async Task GetShareJson_Found_ReturnsRawJson()
    {
        _repository.Setup(r => r.GetShareJson("abc")).ReturnsAsync("{\"schemaVersion\":1}");

        var result = await _classUnderTest.GetShareJson("abc");

        result.Status.Should().Be(HttpStatusCode.OK);
        result.Data.Should().Be("{\"schemaVersion\":1}");
    }

    [Fact]
    public async Task GetShareJson_Missing_Returns404()
    {
        _repository.Setup(r => r.GetShareJson("abc")).ReturnsAsync((string)null);

        var result = await _classUnderTest.GetShareJson("abc");

        result.Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetShareJson_RepositoryThrows_Returns500()
    {
        _repository.Setup(r => r.GetShareJson("abc")).ThrowsAsync(new InvalidOperationException("s3 down"));

        var result = await _classUnderTest.GetShareJson("abc");

        result.Status.Should().Be(HttpStatusCode.InternalServerError);
    }

    #endregion

    #region Helpers

    private sealed class CapturedShare
    {
        public string ShareId { get; set; }
        public BacktestSharePayload Payload { get; set; }
    }

    private CapturedShare GivenCompletedBacktestAndCapturePayload(
        string userId = OwnerId,
        BacktestStatus status = BacktestStatus.Completed)
    {
        var record = new BacktestContextRecord
        {
            Id = BacktestId,
            UserId = userId,
            Status = status,
            Start = "2026-01-05",
            End = "2026-01-06",
            CreatedAt = "2026-01-07T00:00:00Z",
            Request = BuildCreateRequest()
        };

        _repository.Setup(r => r.Get(BacktestId)).ReturnsAsync(record);
        _repository.Setup(r => r.GetPortfolioFromS3(record)).ReturnsAsync(new BacktestResultResponse
        {
            Id = BacktestId,
            CreditsUsed = 42f,
            Hold = new BacktestStrategyPortfolio(),
            High = new BacktestStrategyPortfolio()
        });

        var captured = new CapturedShare();
        _repository.Setup(r => r.PutShare(It.IsAny<string>(), It.IsAny<BacktestSharePayload>()))
            .Callback<string, BacktestSharePayload>((id, payload) =>
            {
                captured.ShareId = id;
                captured.Payload = payload;
            })
            .ReturnsAsync(true);

        return captured;
    }

    private void GivenBenchmarkBars()
    {
        _marketData.Setup(m => m.GetStockDataAsync(It.IsAny<StocksRequest>()))
            .ReturnsAsync(new StocksResponse
            {
                Ticker = "SPY",
                Results =
                [
                    // Deliberately out of order to prove sorting.
                    new Bar { Timestamp = DateTimeOffset.Parse("2026-01-06T00:00:00Z").ToUnixTimeMilliseconds(), Close = 610f },
                    new Bar { Timestamp = DateTimeOffset.Parse("2026-01-05T00:00:00Z").ToUnixTimeMilliseconds(), Close = 600f }
                ]
            });
    }

    private static BacktestCreateRequest BuildCreateRequest() => new()
    {
        Start = DateTimeOffset.Parse("2026-01-05T00:00:00Z"),
        End = DateTimeOffset.Parse("2026-01-06T00:00:00Z"),
        PositionSettings = new StrategyPositionSettings
        {
            StartingBalance = 10000,
            MaxConcurrentPositions = 3,
            Model = new PositionModel { Type = default, Size = 1000 },
            Cooldown = new Timeframe(5, Timespan.minute)
        },
        EntrySettings = new StrategyEntrySettings
        {
            Filters = [SecretFilter]
        },
        ExitSettings = new StrategyExitSettings
        {
            StopLoss = new Exit { Value = 2 },
            TakeProfit = new Exit { Value = 5 },
            TimedExit = new TimedExit { Timeframe = new Timeframe(60, Timespan.minute) }
        }
    };

    #endregion
}
