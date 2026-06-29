using Backtest.Lambda.Services;
using Backtest.Lambda.UnitTests.Fixtures;
using Backtest.Lambda.Utilities;
using FluentAssertions;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Backtest.Lambda.UnitTests.Services;

public class DataCacheUnitTests : IClassFixture<MarketCacheFixture>
{
    private readonly DataCache _classUnderTest;
    private readonly DateTimeOffset _date = DateTimeOffset.Parse("2025-05-27T09:29:00-04:00");

    /// <summary>
    /// SPY has ALL data on this date
    /// W has low pre-market data on this date
    /// AAM has very little data on this date
    /// </summary>
    public DataCacheUnitTests(MarketCacheFixture fixture)
    {
        Skip.If(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AWS_DEPLOYMENT_ROLE")));
        _classUnderTest = fixture.ServiceProvider.GetService<DataCache>();
    }
    
    #region SPY Tests

    [SkippableFact]
    public async Task SPY_Contains_Candles_Before_MarketOpen()
    {
        // Arrange & Act
        await _classUnderTest.Setup(_date, [new Timeframe(1, Timespan.minute)]);

        // Assert
        var stocksResponse = _classUnderTest.GetStocksResponse("SPY", new Timeframe(1, Timespan.minute));
        stocksResponse.Results.Last().Timestamp.Should().Be(_date.ToUnixTimeMilliseconds());
    }

    [SkippableFact]
    public async Task SPY_Contains_30_Candles_Before_MarketOpen()
    {
        // Arrange & Act
        await _classUnderTest.Setup(_date, [new Timeframe(1, Timespan.minute)]);

        // Assert
        var stocksResponse = _classUnderTest.GetStocksResponse("SPY", new Timeframe(1, Timespan.minute));
        var candleRange = stocksResponse.Results.TakeLast(30);
        candleRange.First().Timestamp.Should().Be(_date.AddMinutes(-29).ToUnixTimeMilliseconds());
        candleRange.Last().Timestamp.Should().Be(_date.ToUnixTimeMilliseconds());
    }

    [SkippableFact]
    public async Task SPY_GetNextCandle_Returns_First_MarketOpen_Candle()
    {
        // Arrange
        await _classUnderTest.Setup(_date, [new Timeframe(1, Timespan.minute)]);

        // Act
        var candle = _classUnderTest.GetNextCandle("SPY", 0);

        // Assert
        candle.Timestamp.Should().Be(_date.AddMinutes(1).ToUnixTimeMilliseconds());
    }

    [SkippableFact]
    public async Task SPY_GetNextCandle_Returns_All_MarketOpen_Candles()
    {
        // Arrange
        var marketOpen = _date.AddMinutes(1);
        var marketClose = _date.AddHours(6.5);
        var totalMinutes = (int)(marketClose - marketOpen).TotalMinutes;

        await _classUnderTest.Setup(_date, [new Timeframe(1, Timespan.minute)]);

        // Act & Assert
        for (int i = 0; i < totalMinutes; i++)
        {
            var candle = _classUnderTest.GetNextCandle("SPY", i);
            candle.Timestamp.Should().Be(marketOpen.AddMinutes(i).ToUnixTimeMilliseconds());
        }
    }

    #endregion

    #region W Tests

    [SkippableFact]
    public async Task LowPreMarketDataTicker_Contains_Candles_Before_MarketOpen()
    {
        // Arrange & Act
        await _classUnderTest.Setup(_date, [new Timeframe(1, Timespan.minute)]);

        // Assert
        var stocksResponse = _classUnderTest.GetStocksResponse("W", new Timeframe(1, Timespan.minute));
        stocksResponse.Results.Last().Timestamp.Should().BeLessThan(_date.ToUnixTimeMilliseconds());
    }

    [SkippableFact]
    public async Task LowPreMarketDataTicker_GetNextCandle_Returns_First_MarketOpen_Candle()
    {
        // Arrange
        await _classUnderTest.Setup(_date, [new Timeframe(1, Timespan.minute)]);

        // Act
        var candle = _classUnderTest.GetNextCandle("W", 0);

        // Assert
        candle.Timestamp.Should().Be(_date.AddMinutes(1).ToUnixTimeMilliseconds());
    }

    [SkippableFact]
    public async Task LowPreMarketDataTicker_GetNextCandle_Returns_Some_MarketOpen_Candles()
    {
        // Arrange
        var marketOpen = _date.AddMinutes(1);
        var marketClose = _date.AddHours(6.5);
        var totalMinutes = (int)(marketClose - marketOpen).TotalMinutes;

        await _classUnderTest.Setup(_date, [new Timeframe(1, Timespan.minute)]);

        // Act & Assert
        for (int i = 0; i < totalMinutes; i++)
        {
            var candle = _classUnderTest.GetNextCandle("W", i);
            candle.Timestamp.Should().Be(marketOpen.AddMinutes(i).ToUnixTimeMilliseconds());
        }
    }

    #endregion

    #region AAM Tests

    [SkippableFact]
    public async Task LowDataTicker_Contains_Candles_Before_MarketOpen()
    {
        // Arrange & Act
        await _classUnderTest.Setup(_date, [new Timeframe(1, Timespan.minute)]);

        // Assert
        var stocksResponse = _classUnderTest.GetStocksResponse("AAM", new Timeframe(1, Timespan.minute));
        stocksResponse.Results.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task LowDataTicker_GetNextCandle_Returns_First_MarketOpen_Candle()
    {
        // Arrange
        await _classUnderTest.Setup(_date, [new Timeframe(1, Timespan.minute)]);

        // Act
        var candle = _classUnderTest.GetNextCandle("AAM", 0);

        // Assert
        candle.Timestamp.Should().Be(_date.AddMinutes(1).ToUnixTimeMilliseconds());
    }

    [SkippableFact]
    public async Task LowDataTicker_GetNextCandle_Returns_Some_MarketOpen_Candles()
    {
        // Arrange
        var marketOpen = _date.AddMinutes(1);
        var marketClose = _date.AddHours(6.5);
        var totalMinutes = (int)(marketClose - marketOpen).TotalMinutes;

        await _classUnderTest.Setup(_date, [new Timeframe(1, Timespan.minute)]);

        var stocksResponse = _classUnderTest.GetStocksResponse("AAM", new Timeframe(1, Timespan.minute));

        // Act & Assert
        for (int i = 0; i < totalMinutes; i++)
        {
            var candle = _classUnderTest.GetNextCandle("AAM", i);
            
            if (i == 0 || i == 234)
            {
                candle.Timestamp.Should().Be(marketOpen.AddMinutes(i).ToUnixTimeMilliseconds());
            }
            else
            {
                candle.Should().BeNull();
            }
        }
    }

    #endregion

    [SkippableFact]
    public async Task HourTimeframe_Builds_Initial_Hour_Correctly()
    {
        // Arrange
        var marketOpen = _date.AddMinutes(1);
        var marketClose = _date.AddMinutes(31);
        var totalMinutes = (int)(marketClose - marketOpen).TotalMinutes;

        await _classUnderTest.Setup(_date, [new Timeframe(1, Timespan.minute), new Timeframe(1, Timespan.hour)]);

        var stocksResponse = _classUnderTest.GetStocksResponse("SPY", new Timeframe(1, Timespan.hour));

        // Act & Assert
        for (int i = 0; i < totalMinutes; i++)
        {
            var candle = _classUnderTest.GetNextCandle("SPY", i);
            stocksResponse.UpdateLatestCandle(new Timeframe(1, Timespan.hour), candle);
        }

        // Checked these values from Polygon API directly
        stocksResponse.Results.Last().Open.Should().Be(586.62f);
        stocksResponse.Results.Last().High.Should().Be(587.47f);
        stocksResponse.Results.Last().Low.Should().Be(584.37f);
        stocksResponse.Results.Last().Close.Should().Be(586.13f);

        stocksResponse.Results.Last().Vwap.Should().BeApproximately(586.62f, 1f);

        stocksResponse.Results.Last().Volume.Should().Be(9320298);
        stocksResponse.Results.Last().TransactionCount.Should().Be(116984);
    }
}
