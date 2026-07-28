using FluentAssertions;
using MarketViewer.Api.Services;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market;
using Massive.Client.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MarketViewer.Api.UnitTests.Services;

public class BarCacheServiceUnitTests
{
    private const long MinuteMs = 60_000;
    private static readonly long BaseTs = DateTimeOffset.Parse("2026-07-20T10:00:00-04:00").ToUnixTimeMilliseconds();

    private readonly Mock<IMarketCache> _marketCache = new();
    private readonly BarCacheService _classUnderTest;

    public BarCacheServiceUnitTests()
    {
        _classUnderTest = new BarCacheService(_marketCache.Object, new NullLogger<BarCacheService>());
    }

    [Fact]
    public void Minute_Appends_NoGap_WhenContiguous()
    {
        var response = MinuteResponseEndingAt(BaseTs);

        var result = _classUnderTest.AddBarToCache("SPY", new Timeframe(1, Timespan.minute), BarAt(BaseTs + MinuteMs));

        result.Added.Should().NotBeNull();
        result.HasGap.Should().BeFalse();
        response.Results[^1].Timestamp.Should().Be(BaseTs + MinuteMs);
    }

    [Fact]
    public void Minute_ReportsGap_WhenMinutesSkipped()
    {
        var response = MinuteResponseEndingAt(BaseTs);

        var result = _classUnderTest.AddBarToCache("SPY", new Timeframe(1, Timespan.minute), BarAt(BaseTs + 3 * MinuteMs));

        result.Added.Should().NotBeNull();
        result.HasGap.Should().BeTrue();
        result.GapFromTimestamp.Should().Be(BaseTs);
        result.GapToTimestamp.Should().Be(BaseTs + 3 * MinuteMs);
        response.Results[^1].Timestamp.Should().Be(BaseTs + 3 * MinuteMs);
    }

    [Fact]
    public void Minute_ReturnsDefault_WhenBarNotNewer()
    {
        var response = MinuteResponseEndingAt(BaseTs);

        var result = _classUnderTest.AddBarToCache("SPY", new Timeframe(1, Timespan.minute), BarAt(BaseTs));

        result.Added.Should().BeNull();
        result.HasGap.Should().BeFalse();
        response.Results.Should().HaveCount(2);
    }

    [Fact]
    public void Backfill_InsertsMissingBars_InOrder_WithinExclusiveBounds()
    {
        var response = MinuteResponseEndingAt(BaseTs);
        response.Results.Add(BarAt(BaseTs + 3 * MinuteMs)); // gap: minutes +1, +2 missing

        var candidates = new[]
        {
            BarAt(BaseTs),                 // at lower bound: excluded
            BarAt(BaseTs + MinuteMs),
            BarAt(BaseTs + 2 * MinuteMs),
            BarAt(BaseTs + 3 * MinuteMs)   // at upper bound: excluded
        };

        var inserted = _classUnderTest.BackfillMinuteBars("SPY", candidates, BaseTs, BaseTs + 3 * MinuteMs);

        inserted.Should().Be(2);
        response.Results.Should().HaveCount(5);
        response.Results.Should().BeInAscendingOrder(bar => bar.Timestamp);
        response.Results.Select(bar => bar.Timestamp).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Backfill_SkipsMinutesAlreadyPresent()
    {
        var response = MinuteResponseEndingAt(BaseTs);
        response.Results.Add(BarAt(BaseTs + MinuteMs));
        response.Results.Add(BarAt(BaseTs + 3 * MinuteMs));

        var candidates = new[] { BarAt(BaseTs + MinuteMs), BarAt(BaseTs + 2 * MinuteMs) };

        var inserted = _classUnderTest.BackfillMinuteBars("SPY", candidates, BaseTs, BaseTs + 3 * MinuteMs);

        inserted.Should().Be(1);
        response.Results.Select(bar => bar.Timestamp).Should().OnlyHaveUniqueItems();
        response.Results.Should().BeInAscendingOrder(bar => bar.Timestamp);
    }

    [Fact]
    public void Backfill_ReturnsZero_WhenNoCachedSeries()
    {
        var inserted = _classUnderTest.BackfillMinuteBars("SPY", [BarAt(BaseTs + MinuteMs)], BaseTs, BaseTs + 2 * MinuteMs);

        inserted.Should().Be(0);
    }

    private StocksResponse MinuteResponseEndingAt(long lastTimestamp)
    {
        var response = new StocksResponse
        {
            Ticker = "SPY",
            Results = [BarAt(lastTimestamp - MinuteMs), BarAt(lastTimestamp)]
        };

        _marketCache
            .Setup(m => m.GetStocksResponse("SPY",
                It.Is<Timeframe>(t => t.Multiplier == 1 && t.Timespan == Timespan.minute),
                It.IsAny<DateTimeOffset>()))
            .Returns(response);

        return response;
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
}
