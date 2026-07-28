using FluentAssertions;
using MarketViewer.Api.Config;
using MarketViewer.Api.HostedServices;
using MarketViewer.Api.Jobs;
using MarketViewer.Api.Services;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Infrastructure.Config;
using Massive.Client.Interfaces;
using Massive.Client.Models;
using Massive.Client.Requests;
using Massive.Client.Responses;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
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
    private static readonly long BaseTs = DateTimeOffset.Parse("2026-07-20T10:00:00-04:00").ToUnixTimeMilliseconds();

    // With DelayMinutes = 0 the probe expects the wall-clock n-1 bar.
    private static long ExpectedBarStart() =>
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 60_000 * 60_000 - 60_000;

    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly Mock<IMassiveClient> _massiveClient = new();
    private readonly Mock<IJobExecutionContext> _jobContext = new();
    private readonly Mock<ILogger<SnapshotJob>> _logger = new();
    private readonly MemoryMarketCache _marketCache;
    private readonly CacheWarmupState _warmupState;
    private readonly SnapshotJob _classUnderTest;

    public SnapshotJobUnitTests()
    {
        _marketCache = new MemoryMarketCache(_memoryCache, new Mock<Amazon.S3.IAmazonS3>().Object);

        var meterFactory = new Mock<IMeterFactory>();
        meterFactory.Setup(f => f.Create(It.IsAny<MeterOptions>())).Returns(new Meter("test"));
        _warmupState = new CacheWarmupState(meterFactory.Object);

        // Attempt cap of 3 keeps probe-count assertions stable even if the wall
        // clock crosses a minute boundary mid-test (the "fresh" bar goes stale and
        // the probe exhausts at the same count).
        var config = new SnapshotConfig { ProbeMaxAttempts = 3, ProbeDelayMs = 1, BackfillMaxTickers = 50, RestBackfillMaxGapMinutes = 5 };
        var marketDataConfig = new MarketDataConfig { DelayMinutes = 0 };

        _classUnderTest = new SnapshotJob(
            config,
            marketDataConfig,
            _massiveClient.Object,
            _marketCache,
            _warmupState,
            new BarCacheService(_marketCache, new NullLogger<BarCacheService>()),
            _logger.Object);
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
    public async Task Probe_RetriesUntilExpectedBarFlushed_ThenAppliesSnapshot()
    {
        var expected = ExpectedBarStart();

        _warmupState.MarkReady();
        SeedSpyMinuteSeriesEndingAt(expected - MinuteMs);

        _massiveClient
            .SetupSequence(m => m.GetAllTickersSnapshot("SPY", false))
            .ReturnsAsync(SnapshotWithSpyMinute(expected - MinuteMs)) // previous bar still served
            .ReturnsAsync(SnapshotWithSpyMinute(expected - MinuteMs))
            .ReturnsAsync(SnapshotWithSpyMinute(expected));           // expected bar flushed

        _massiveClient
            .Setup(m => m.GetAllTickersSnapshot(null, false))
            .ReturnsAsync(SnapshotWithSpyMinute(expected));

        await _classUnderTest.Execute(_jobContext.Object);

        _massiveClient.Verify(m => m.GetAllTickersSnapshot("SPY", false), Times.Exactly(3));
        _massiveClient.Verify(m => m.GetAllTickersSnapshot(null, false), Times.Once);

        SpyMinuteSeries().Results[^1].Timestamp.Should().Be(expected);
    }

    [Fact]
    public async Task Probe_Exhausted_StillAppliesFullSnapshot()
    {
        var expected = ExpectedBarStart();

        _warmupState.MarkReady();
        SeedSpyMinuteSeriesEndingAt(expected - 2 * MinuteMs);

        // Provider never flushes the expected bar within the probe window.
        _massiveClient
            .Setup(m => m.GetAllTickersSnapshot("SPY", false))
            .ReturnsAsync(SnapshotWithSpyMinute(expected - MinuteMs));

        _massiveClient
            .Setup(m => m.GetAllTickersSnapshot(null, false))
            .ReturnsAsync(SnapshotWithSpyMinute(expected - MinuteMs));

        await _classUnderTest.Execute(_jobContext.Object);

        _massiveClient.Verify(m => m.GetAllTickersSnapshot("SPY", false), Times.Exactly(3));
        _massiveClient.Verify(m => m.GetAllTickersSnapshot(null, false), Times.Once);
        SpyMinuteSeries().Results[^1].Timestamp.Should().Be(expected - MinuteMs);
    }

    [Fact]
    public async Task Diff_FallsBackToCurrentLiveBar_WhenNotYetRolledIntoRing()
    {
        _warmupState.MarkReady();
        SeedSpyMinuteSeriesEndingAt(BaseTs);

        // One traded minute, no later tick: the completed bar is still the live bar.
        AddWebsocketMinute(BaseTs + MinuteMs);

        _massiveClient
            .Setup(m => m.GetAllTickersSnapshot("SPY", false))
            .ReturnsAsync(SnapshotWithSpyMinute(BaseTs + MinuteMs));
        _massiveClient
            .Setup(m => m.GetAllTickersSnapshot(null, false))
            .ReturnsAsync(SnapshotWithSpyMinute(BaseTs + MinuteMs));

        await _classUnderTest.Execute(_jobContext.Object);

        GetWideEventValue("wsMatched").Should().Be(1);
        GetWideEventValue("wsMissing").Should().Be(0);
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

    [Fact]
    public async Task LongGap_AssumedNaturalIlliquidity_SkipsRest()
    {
        _warmupState.MarkReady();
        SeedSpyMinuteSeriesEndingAt(BaseTs);

        // 10 missing minutes > RestBackfillMaxGapMinutes (5): a thin ticker that
        // simply didn't trade — no REST call.
        _massiveClient
            .Setup(m => m.GetAllTickersSnapshot("SPY", false))
            .ReturnsAsync(SnapshotWithSpyMinute(BaseTs + 11 * MinuteMs));
        _massiveClient
            .Setup(m => m.GetAllTickersSnapshot(null, false))
            .ReturnsAsync(SnapshotWithSpyMinute(BaseTs + 11 * MinuteMs));

        await _classUnderTest.Execute(_jobContext.Object);

        SpyMinuteSeries().Results[^1].Timestamp.Should().Be(BaseTs + 11 * MinuteMs);
        _massiveClient.Verify(m => m.GetAggregates(It.IsAny<MassiveAggregateRequest>()), Times.Never);
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

    /// <summary>
    /// Pulls a named value out of the SNAPSHOT_RUN structured log state.
    /// </summary>
    private long GetWideEventValue(string key)
    {
        var state = _logger.Invocations
            .Where(invocation => invocation.Method.Name == nameof(ILogger.Log))
            .Select(invocation => invocation.Arguments[2])
            .OfType<IReadOnlyList<KeyValuePair<string, object>>>()
            .FirstOrDefault(values => values.Any(kv => kv.Key == key));

        state.Should().NotBeNull($"a SNAPSHOT_RUN log with field '{key}' should have been emitted");

        return Convert.ToInt64(state!.First(kv => kv.Key == key).Value);
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
