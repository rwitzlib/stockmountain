using FluentAssertions;
using MarketViewer.Api.Controllers.Market;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Requests.Market;
using MarketViewer.Contracts.Responses.Market;
using Massive.Client.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace MarketViewer.Api.UnitTests.Controllers;

public class LivePricesControllerUnitTests
{
    private const long MinuteMs = 60_000;
    private static readonly long BaseTs = DateTimeOffset.Parse("2026-08-13T10:00:00-04:00").ToUnixTimeMilliseconds();

    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly MemoryMarketCache _marketCache;
    private readonly LivePricesController _classUnderTest;

    public LivePricesControllerUnitTests()
    {
        _marketCache = new MemoryMarketCache(_memoryCache, new Mock<Amazon.S3.IAmazonS3>().Object);
        _classUnderTest = new LivePricesController(_marketCache);
    }

    [Fact]
    public void FormingBar_ReturnsItsClose()
    {
        AddTick("TEST", BaseTs, close: 101.5f);

        var response = GetPrices("TEST");

        var price = response.Prices.Should().ContainSingle().Subject;
        price.Ticker.Should().Be("TEST");
        price.Price.Should().Be(101.5f);
        price.Timestamp.Should().Be(BaseTs);
        price.FromFormingBar.Should().BeTrue();
    }

    [Fact]
    public void FormingBar_TracksLatestTick()
    {
        AddTick("TEST", BaseTs, close: 101.5f);
        AddTick("TEST", BaseTs + 10_000, close: 99.2f);

        var response = GetPrices("TEST");

        response.Prices.Should().ContainSingle().Which.Price.Should().Be(99.2f);
    }

    [Fact]
    public void NoFormingBar_FallsBackToNewestCompletedBar()
    {
        // Two ticks a minute apart roll the first bar into the ring; dropping the
        // forming-bar cache entry simulates a ticker with no trades this minute.
        AddTick("TEST", BaseTs, close: 101.5f);
        AddTick("TEST", BaseTs + MinuteMs, close: 102.5f);
        _memoryCache.Remove("TEST");

        var response = GetPrices("TEST");

        var price = response.Prices.Should().ContainSingle().Subject;
        price.Price.Should().Be(101.5f);
        price.Timestamp.Should().Be(BaseTs);
        price.FromFormingBar.Should().BeFalse();
    }

    [Fact]
    public void UnknownTicker_IsOmitted()
    {
        AddTick("TEST", BaseTs, close: 101.5f);

        var response = GetPrices("TEST", "MISSING");

        response.Prices.Should().ContainSingle().Which.Ticker.Should().Be("TEST");
    }

    [Fact]
    public void EmptyRequest_ReturnsBadRequest()
    {
        var result = _classUnderTest.GetPrices(new LivePricesRequest());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    private LivePricesResponse GetPrices(params string[] tickers)
    {
        var result = _classUnderTest.GetPrices(new LivePricesRequest { Tickers = [.. tickers] });

        return result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<LivePricesResponse>().Subject;
    }

    private void AddTick(string ticker, long tickStart, float close)
    {
        _marketCache.AddLiveBar(new MassiveWebsocketAggregateResponse
        {
            Ticker = ticker,
            TickStart = tickStart,
            Open = close,
            High = close,
            Low = close,
            Close = close,
            Volume = 100,
            TickVwap = close
        });
    }
}
