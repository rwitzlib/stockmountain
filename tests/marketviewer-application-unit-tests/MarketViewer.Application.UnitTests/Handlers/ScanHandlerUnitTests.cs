using Amazon;
using Amazon.S3;
using FluentAssertions;
using MarketViewer.Application.Handlers.Data.Tickers;
using MarketViewer.Application.Handlers.Market.Scan;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Requests.Market.Scan;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Massive.Client.Models;
using Massive.Client.Responses;
using Xunit;

namespace MarketViewer.Application.UnitTests.Handlers;

public class ScanHandlerUnitTests
{
    private readonly ScanHandler _classUnderTest;
    private readonly MemoryMarketCache _marketCache;

    public ScanHandlerUnitTests()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var s3Client = new AmazonS3Client(RegionEndpoint.USEast2);

        _marketCache = new MemoryMarketCache(memoryCache, s3Client);

        _classUnderTest = new ScanHandler(_marketCache, new IndicatorExpressionEngine() , new NullLogger<ScanHandler>());
    }

    [Fact]
    public async Task LatestBarIsIncludedAndPassesFilters()
    {
        // Arrange
        var now = DateTimeOffset.Now;
        SetupMarketCacheForMinuteTimeframe(now, true, true);

        var request = new ScanRequest
        {
            Timestamp = now,
            Filters =
            [
                "close > 5 [1m]"
            ]
        };

        // Act
        var response = await _classUnderTest.Handle(request, default);

        // Assert
        response.Data.Items.Count().Should().Be(1);
    }

    [Fact]
    public async Task LatestBarIsIncludedAndDoesntPassFilters()
    {
        // Arrange
        var now = DateTimeOffset.Now;
        SetupMarketCacheForMinuteTimeframe(now, true, false);

        var request = new ScanRequest
        {
            Timestamp = now,
            Filters =
            [
                "close > 5 [1m]"
            ]
        };

        // Act
        var response = await _classUnderTest.Handle(request, default);

        // Assert
        response.Data.Items.Count().Should().Be(0);
    }

    [Fact]
    public async Task LatestBarIsNotIncluded_ShouldReturnItems()
    {
        // Arrange
        var now = DateTimeOffset.Now;
        SetupMarketCacheForMinuteTimeframe(now, false);

        var request = new ScanRequest
        {
            Timestamp = now,
            Filters =
            [
                "close >= 5 [1m]"
            ]
        };

        // Act
        var response = await _classUnderTest.Handle(request, default);

        // Assert
        response.Data.Items.Count().Should().Be(1);
    }

    [Fact]
    public async Task LatestBarIsNotIncluded_ShouldNotReturnItems()
    {
        // Arrange
        var now = DateTimeOffset.Now;
        SetupMarketCacheForMinuteTimeframe(now, false);

        var request = new ScanRequest
        {
            Timestamp = now,
            Filters =
            [
                "close > 5 [1m]"
            ]
        };

        // Act
        var response = await _classUnderTest.Handle(request, default);

        // Assert
        response.Data.Items.Count().Should().Be(0);
    }

    [Fact]
    public async Task CompletedBarsOnly_ExcludesPartialLiveBar()
    {
        // The live (partial) bar would pass the filter, but completed-bar mode
        // must not evaluate it.
        var now = DateTimeOffset.Now;
        SetupMarketCacheForMinuteTimeframe(now, true, true);

        var request = new ScanRequest
        {
            Timestamp = now,
            CompletedBarsOnly = true,
            Filters =
            [
                "close > 5 [1m]"
            ]
        };

        var response = await _classUnderTest.Handle(request, default);

        response.Data.Items.Count().Should().Be(0);
    }

    [Fact]
    public async Task CompletedBarsOnly_StillEvaluatesRingCompletedBar()
    {
        // A completed minute that only exists in the websocket ring (snapshot poll
        // not landed yet) passes the filter; the newer partial bar would fail it.
        var now = DateTimeOffset.Now;
        SetupMarketCacheForMinuteTimeframe(now.AddMinutes(-2), false);

        _marketCache.AddLiveBar(new MassiveWebsocketAggregateResponse
        {
            Ticker = "SPY",
            Close = 1000, // completed bar: passes "close > 5"
            TickStart = now.AddMinutes(-1).ToUnixTimeMilliseconds()
        });
        _marketCache.AddLiveBar(new MassiveWebsocketAggregateResponse
        {
            Ticker = "SPY",
            Close = 0, // in-progress bar: would fail the filter if evaluated
            TickStart = now.ToUnixTimeMilliseconds()
        });

        var request = new ScanRequest
        {
            Timestamp = now,
            CompletedBarsOnly = true,
            Filters =
            [
                "close > 5 [1m]"
            ]
        };

        var response = await _classUnderTest.Handle(request, default);

        response.Data.Items.Count().Should().Be(1);
    }

    [Fact]
    public async Task LatestBarIsIncluded_Passes_Multiple_Timeframes()
    {
        // Arrange
        var now = DateTimeOffset.Now;
        SetupMarketCacheForMinuteTimeframe(now, true, true);
        SetupMarketCacheForHourTimeframe(now, true, true);

        var request = new ScanRequest
        {
            Timestamp = now,
            Filters =
            [
                "close >= 5 [1m]",
                "volume > 5 [1h]"
            ]
        };

        // Act
        var response = await _classUnderTest.Handle(request, default);

        // Assert
        response.Data.Items.Count().Should().Be(1);
    }

    #region Private Methods

    private void SetupMarketCacheForMinuteTimeframe(DateTimeOffset timestamp, bool includeLatestBar, bool latestBarShouldPassFilters = true)
    {
        _marketCache.SetTickers(["SPY"]);
        _marketCache.SetTickersByTimeframe(timestamp, new Timeframe(1, Timespan.minute), ["SPY"]);

        List<Bar> results = [];
        for (int i = 30; i > 0; i--)
        {
            results.Add(new Bar
            {
                Timestamp = timestamp.AddMinutes(-i).ToUnixTimeMilliseconds(),
                Close = 5
            });
        }

        _marketCache.SetStocksResponse(new StocksResponse
        {
            Ticker = "SPY",
            Results = results
        }, new Timeframe(1, Timespan.minute), timestamp);

        if (!includeLatestBar)
        {
            return;
        }

        var bar = _marketCache.GetLiveBar("SPY");

        if (bar is null)
        {
            _marketCache.AddLiveBar(new MassiveWebsocketAggregateResponse
            {
                Ticker = "SPY",
                Close = latestBarShouldPassFilters ? 1000 : 0,
                TickStart = timestamp.ToUnixTimeMilliseconds()
            });
        }
        else
        {
            bar.Close = latestBarShouldPassFilters ? 1000 : 0;
        }
    }

    private void SetupMarketCacheForHourTimeframe(DateTimeOffset timestamp, bool includeLatestBar, bool latestBarShouldPassFilters = true)
    {
        _marketCache.SetTickers(["SPY"]);
        _marketCache.SetTickersByTimeframe(timestamp, new Timeframe(1, Timespan.hour), ["SPY"]);

        var nearestHour = new DateTimeOffset(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, 0, 0, timestamp.Offset);

        List<Bar> results = [];
        for (int i = 30; i >= 0; i--)
        {
            results.Add(new Bar
            {
                Timestamp = nearestHour.AddHours(-i).ToUnixTimeMilliseconds(),
                Volume = 1
            });
        }

        _marketCache.SetStocksResponse(new StocksResponse
        {
            Ticker = "SPY",
            Results = results
        }, new Timeframe(1, Timespan.hour), timestamp);

        if (!includeLatestBar)
        {
            return;
        }

        var bar = _marketCache.GetLiveBar("SPY");

        if (bar is null)
        {
            _marketCache.AddLiveBar(new MassiveWebsocketAggregateResponse
            {
                Ticker = "SPY",
                Volume = latestBarShouldPassFilters ? 1000 : 0,
                TickStart = timestamp.ToUnixTimeMilliseconds()
            });
        }
        else
        {
            bar.Volume = latestBarShouldPassFilters ? 1000 : 0;
        }
    }
    #endregion
}
