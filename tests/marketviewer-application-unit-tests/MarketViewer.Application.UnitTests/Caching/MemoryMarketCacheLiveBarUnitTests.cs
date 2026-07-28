using Amazon;
using Amazon.S3;
using FluentAssertions;
using MarketViewer.Contracts.Caching;
using Massive.Client.Responses;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace MarketViewer.Application.UnitTests.Caching;

public class MemoryMarketCacheLiveBarUnitTests
{
    private readonly MemoryMarketCache _classUnderTest;

    private static readonly long MinuteMs = 60_000;
    private static readonly long BaseTs = DateTimeOffset.Parse("2026-07-20T10:00:00-04:00").ToUnixTimeMilliseconds();

    public MemoryMarketCacheLiveBarUnitTests()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var s3Client = new AmazonS3Client(RegionEndpoint.USEast2);

        _classUnderTest = new MemoryMarketCache(memoryCache, s3Client);
    }

    [Fact]
    public void LiveBar_TimestampFlooredToMinute_WhenFirstTickIsMidMinute()
    {
        // A thin ticker's first per-second aggregate can start at :03; the bar must
        // still be stamped at the minute boundary to line up with snapshot bars.
        AddTick(BaseTs + 3_000, close: 10f, high: 10f, low: 10f, volume: 5f);

        _classUnderTest.GetLiveBar("SPY").Timestamp.Should().Be(BaseTs);

        AddTick(BaseTs + MinuteMs + 7_000, close: 11f, high: 11f, low: 11f, volume: 5f);

        var ring = _classUnderTest.GetRecentLiveBars("SPY");
        ring.Should().HaveCount(1);
        ring[0].Timestamp.Should().Be(BaseTs);
        _classUnderTest.GetLiveBar("SPY").Timestamp.Should().Be(BaseTs + MinuteMs);
    }

    [Fact]
    public void GetRecentLiveBars_ReturnsEmpty_WhenNothingStreamed()
    {
        var bars = _classUnderTest.GetRecentLiveBars("SPY");

        bars.Should().NotBeNull();
        bars.Should().BeEmpty();
    }

    [Fact]
    public void Rollover_PushesCompletedBarIntoRing()
    {
        // Two ticks in minute 0, then a tick in minute 1 triggers rollover.
        AddTick(BaseTs, close: 10f, high: 11f, low: 9f, volume: 100f);
        AddTick(BaseTs + 30_000, close: 10.5f, high: 12f, low: 8f, volume: 50f);
        AddTick(BaseTs + MinuteMs, close: 10.6f, high: 10.6f, low: 10.6f, volume: 10f);

        var ring = _classUnderTest.GetRecentLiveBars("SPY");

        ring.Should().HaveCount(1);
        ring[0].Timestamp.Should().Be(BaseTs);
        ring[0].Close.Should().Be(10.5f);
        ring[0].High.Should().Be(12f);
        ring[0].Low.Should().Be(8f);
        ring[0].Volume.Should().Be(150f);

        // The in-progress bar is the new minute, not part of the ring.
        _classUnderTest.GetLiveBar("SPY").Timestamp.Should().Be(BaseTs + MinuteMs);
    }

    [Fact]
    public void Ring_IsBoundedToFiveBars_OldestDropped()
    {
        for (var minute = 0; minute < 8; minute++)
        {
            AddTick(BaseTs + minute * MinuteMs, close: minute, high: minute, low: minute, volume: 1f);
        }

        var ring = _classUnderTest.GetRecentLiveBars("SPY");

        // 8 minutes streamed -> 7 completed -> last 5 kept: minutes 2..6.
        ring.Should().HaveCount(5);
        ring[0].Timestamp.Should().Be(BaseTs + 2 * MinuteMs);
        ring[^1].Timestamp.Should().Be(BaseTs + 6 * MinuteMs);
    }

    [Fact]
    public void Ring_IsOrderedOldestToNewest()
    {
        for (var minute = 0; minute < 4; minute++)
        {
            AddTick(BaseTs + minute * MinuteMs, close: minute, high: minute, low: minute, volume: 1f);
        }

        var ring = _classUnderTest.GetRecentLiveBars("SPY");

        ring.Should().HaveCount(3);
        ring.Should().BeInAscendingOrder(bar => bar.Timestamp);
    }

    [Fact]
    public void Ring_ReadersGetStableSnapshot_WhileRolloversContinue()
    {
        AddTick(BaseTs, close: 1f, high: 1f, low: 1f, volume: 1f);
        AddTick(BaseTs + MinuteMs, close: 2f, high: 2f, low: 2f, volume: 1f);

        var snapshot = _classUnderTest.GetRecentLiveBars("SPY");
        snapshot.Should().HaveCount(1);

        AddTick(BaseTs + 2 * MinuteMs, close: 3f, high: 3f, low: 3f, volume: 1f);

        // The previously handed-out list must not have been mutated in place.
        snapshot.Should().HaveCount(1);
        _classUnderTest.GetRecentLiveBars("SPY").Should().HaveCount(2);
    }

    private void AddTick(long tickStart, float close, float high, float low, float volume)
    {
        _classUnderTest.AddLiveBar(new MassiveWebsocketAggregateResponse
        {
            Ticker = "SPY",
            TickStart = tickStart,
            Close = close,
            High = high,
            Low = low,
            Open = close,
            Volume = volume,
            TickVwap = close
        });
    }
}
