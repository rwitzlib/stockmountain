using FluentAssertions;
using MarketViewer.Api.Config;
using MarketViewer.Api.HostedServices;
using MarketViewer.Api.Jobs;
using MarketViewer.Api.Services;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market;
using Massive.Client.Interfaces;
using Massive.Client.Models;
using Massive.Client.Requests;
using Massive.Client.Responses;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Quartz;
using System.Diagnostics.Metrics;
using Xunit;
using Bar = Massive.Client.Models.Bar;

namespace MarketViewer.Api.UnitTests.Jobs;

public class SnapshotJobUnitTests
{
    private const long MinuteMs = 60_000;
    private const string LastSpyMinuteKey = "SnapshotJob/LastSpyMinuteTs";
    private static readonly long BaseTs = DateTimeOffset.Parse("2026-07-20T10:00:00-04:00").ToUnixTimeMilliseconds();

    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly Mock<IMassiveClient> _massiveClient = new();
    private readonly Mock<IJobExecutionContext> _jobContext = new();
    private readonly MemoryMarketCache _marketCache;
    private readonly CacheWarmupState _warmupState;
    private readonly SnapshotJob _classUnderTest;

    public SnapshotJobUnitTests()
    {
        _marketCache = new MemoryMarketCache(_memoryCache, new Mock<Amazon.S3.IAmazonS3>().Object);

        var meterFactory = new Mock<IMeterFactory>();
        meterFactory.Setup(f => f.Create(It.IsAny<MeterOptions>())).Returns(new Meter("test"));
        _warmupState = new CacheWarmupState(meterFactory.Object);

        var config = new SnapshotConfig { ProbeMaxAttempts = 3, ProbeDelayMs = 1, BackfillMaxTickers = 50 };

        _classUnderTest = new SnapshotJob(
            config,
            _memoryCache,
            _massiveClient.Object,
            _marketCache,
            _warmupState,
            new BarCacheService(_marketCache, new NullLogger<BarCacheService>()),
            new NullLogger<SnapshotJob>());
    }

    [Fact]
    public async Task DuringWarmup_BuffersSnapshot_WithoutProbing()
    {
        _massiveClient
            .Setup(m => m.GetAllTickersSnapshot(null, false))
            .ReturnsAsync(SnapshotWithSpyMinute(BaseTs));

        await _classUnderTest.Execute(_jobContext.Object);

        _warmupState.BufferedCount.Should().Be(1);
        _massiveClient.Verify(m => m.GetAllTickersSnapshot("SPY", false), Times.Never);
    }

    [Fact]
    public async Task Probe_RetriesUntilSpyBarAdvances_ThenAppliesSnapshot()
    {
        _warmupState.MarkReady();
        SeedSpyMinuteSeriesEndingAt(BaseTs);
        _memoryCache.Set(LastSpyMinuteKey, BaseTs);

        _massiveClient
            .SetupSequence(m => m.GetAllTickersSnapshot("SPY", false))
            .ReturnsAsync(SnapshotWithSpyMinute(BaseTs))          // stale
            .ReturnsAsync(SnapshotWithSpyMinute(BaseTs))          // stale
            .ReturnsAsync(SnapshotWithSpyMinute(BaseTs + MinuteMs)); // advanced

        _massiveClient
            .Setup(m => m.GetAllTickersSnapshot(null, false))
            .ReturnsAsync(SnapshotWithSpyMinute(BaseTs + MinuteMs));

        await _classUnderTest.Execute(_jobContext.Object);

        _massiveClient.Verify(m => m.GetAllTickersSnapshot("SPY", false), Times.Exactly(3));
        _massiveClient.Verify(m => m.GetAllTickersSnapshot(null, false), Times.Once);

        SpyMinuteSeries().Results[^1].Timestamp.Should().Be(BaseTs + MinuteMs);
        _memoryCache.Get<long>(LastSpyMinuteKey).Should().Be(BaseTs + MinuteMs);
    }

    [Fact]
    public async Task Probe_Exhausted_StillAppliesFullSnapshot()
    {
        _warmupState.MarkReady();
        SeedSpyMinuteSeriesEndingAt(BaseTs);
        _memoryCache.Set(LastSpyMinuteKey, BaseTs + MinuteMs);

        // Probe never advances past what was already applied.
        _massiveClient
            .Setup(m => m.GetAllTickersSnapshot("SPY", false))
            .ReturnsAsync(SnapshotWithSpyMinute(BaseTs + MinuteMs));

        _massiveClient
            .Setup(m => m.GetAllTickersSnapshot(null, false))
            .ReturnsAsync(SnapshotWithSpyMinute(BaseTs + MinuteMs));

        await _classUnderTest.Execute(_jobContext.Object);

        _massiveClient.Verify(m => m.GetAllTickersSnapshot("SPY", false), Times.Exactly(3));
        _massiveClient.Verify(m => m.GetAllTickersSnapshot(null, false), Times.Once);
        SpyMinuteSeries().Results[^1].Timestamp.Should().Be(BaseTs + MinuteMs);
    }

    [Fact]
    public async Task Gap_BackfilledFromWebsocketRing_WithoutRestCalls()
    {
        _warmupState.MarkReady();
        SeedSpyMinuteSeriesEndingAt(BaseTs);

        // Websocket stayed alive across two missed polls: ring holds +1m and +2m.
        AddWebsocketMinute(BaseTs + MinuteMs);
        AddWebsocketMinute(BaseTs + 2 * MinuteMs);
        AddWebsocketMinute(BaseTs + 3 * MinuteMs); // in-progress bar, rolls the others into the ring

        _massiveClient
            .Setup(m => m.GetAllTickersSnapshot("SPY", false))
            .ReturnsAsync(SnapshotWithSpyMinute(BaseTs + 3 * MinuteMs));
        _massiveClient
            .Setup(m => m.GetAllTickersSnapshot(null, false))
            .ReturnsAsync(SnapshotWithSpyMinute(BaseTs + 3 * MinuteMs));

        await _classUnderTest.Execute(_jobContext.Object);

        var series = SpyMinuteSeries().Results;
        series.Select(bar => bar.Timestamp).Should().ContainInOrder(
            BaseTs, BaseTs + MinuteMs, BaseTs + 2 * MinuteMs, BaseTs + 3 * MinuteMs);
        series.Select(bar => bar.Timestamp).Should().OnlyHaveUniqueItems();

        _massiveClient.Verify(m => m.GetAggregates(It.IsAny<MassiveAggregateRequest>()), Times.Never);
    }

    [Fact]
    public async Task Gap_FallsBackToRestAggregates_WhenRingIsEmpty()
    {
        _warmupState.MarkReady();
        SeedSpyMinuteSeriesEndingAt(BaseTs);

        _massiveClient
            .Setup(m => m.GetAllTickersSnapshot("SPY", false))
            .ReturnsAsync(SnapshotWithSpyMinute(BaseTs + 3 * MinuteMs));
        _massiveClient
            .Setup(m => m.GetAllTickersSnapshot(null, false))
            .ReturnsAsync(SnapshotWithSpyMinute(BaseTs + 3 * MinuteMs));

        _massiveClient
            .Setup(m => m.GetAggregates(It.Is<MassiveAggregateRequest>(r => r.Ticker == "SPY")))
            .ReturnsAsync(new MassiveAggregateResponse
            {
                Ticker = "SPY",
                Results = [BarAt(BaseTs + MinuteMs), BarAt(BaseTs + 2 * MinuteMs)]
            });

        await _classUnderTest.Execute(_jobContext.Object);

        var series = SpyMinuteSeries().Results;
        series.Select(bar => bar.Timestamp).Should().ContainInOrder(
            BaseTs, BaseTs + MinuteMs, BaseTs + 2 * MinuteMs, BaseTs + 3 * MinuteMs);

        _massiveClient.Verify(m => m.GetAggregates(It.IsAny<MassiveAggregateRequest>()), Times.Once);
    }

    #region Private Methods

    private void SeedSpyMinuteSeriesEndingAt(long lastTimestamp)
    {
        _marketCache.SetStocksResponse(new StocksResponse
        {
            Ticker = "SPY",
            Results = [BarAt(lastTimestamp - MinuteMs), BarAt(lastTimestamp)]
        }, new Timeframe(1, Timespan.minute), DateTimeOffset.Now);
    }

    private StocksResponse SpyMinuteSeries()
    {
        return _marketCache.GetStocksResponse("SPY", new Timeframe(1, Timespan.minute), DateTimeOffset.Now);
    }

    private void AddWebsocketMinute(long tickStart)
    {
        _marketCache.AddLiveBar(new MassiveWebsocketAggregateResponse
        {
            Ticker = "SPY",
            TickStart = tickStart,
            Open = 10f,
            Close = 10f,
            High = 11f,
            Low = 9f,
            Volume = 100f,
            TickVwap = 10f
        });
    }

    private static MassiveSnapshotResponse SnapshotWithSpyMinute(long timestamp)
    {
        return new MassiveSnapshotResponse
        {
            Tickers =
            [
                new Snapshot
                {
                    Ticker = "SPY",
                    Minute = BarAt(timestamp)
                }
            ]
        };
    }

    private static Bar BarAt(long timestamp)
    {
        return new Bar
        {
            Timestamp = timestamp,
            Open = 10f,
            Close = 10f,
            High = 11f,
            Low = 9f,
            Volume = 100f,
            Vwap = 10f
        };
    }

    #endregion
}
