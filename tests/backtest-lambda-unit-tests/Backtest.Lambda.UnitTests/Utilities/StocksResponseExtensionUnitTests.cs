using Backtest.Lambda.Utilities;
using FluentAssertions;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market;
using Massive.Client.Models;

namespace Backtest.Lambda.UnitTests.Utilities;

public class StocksResponseExtensionUnitTests
{
    #region One Minute Timeframe Tests

    [Fact]
    public void UpdateLatestCandle_OneMinuteTimeframe_AddsCandleDirectly()
    {
        // Arrange
        var stocksResponse = new StocksResponse
        {
            Results = new List<Bar>
            {
                CreateBar(1000, 100f, 95f, 98f, 97f, 1000, 10)
            }
        };
        var timeframe = new Timeframe(1, Timespan.minute);
        var nextCandle = CreateBar(2000, 102f, 96f, 99f, 98f, 2000, 20);

        // Act
        stocksResponse.UpdateLatestCandle(timeframe, nextCandle);

        // Assert
        stocksResponse.Results.Should().HaveCount(2);
        stocksResponse.Results.Last().Should().Be(nextCandle);
    }

    [Fact]
    public void UpdateLatestCandle_OneMinuteTimeframe_WithEmptyResults_AddsCandle()
    {
        // Arrange
        var stocksResponse = new StocksResponse
        {
            Results = new List<Bar>()
        };
        var timeframe = new Timeframe(1, Timespan.minute);
        var nextCandle = CreateBar(1000, 100f, 95f, 98f, 97f, 1000, 10);

        // Act
        stocksResponse.UpdateLatestCandle(timeframe, nextCandle);

        // Assert
        stocksResponse.Results.Should().HaveCount(1);
        stocksResponse.Results.First().Should().Be(nextCandle);
    }

    #endregion

    #region New Candle Tests (Minute Timeframe)

    [Fact]
    public void UpdateLatestCandle_MinuteTimeframe_NewCandle_WhenTimestampAfterCandleEnd_AddsNewCandle()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 1, 9, 30, 0, TimeSpan.Zero);
        var lastCandle = CreateBar(baseTime.ToUnixTimeMilliseconds(), 100f, 95f, 98f, 97f, 1000, 10);
        var stocksResponse = new StocksResponse
        {
            Results = new List<Bar> { lastCandle }
        };
        var timeframe = new Timeframe(5, Timespan.minute);
        var nextCandle = CreateBar(baseTime.AddMinutes(5).ToUnixTimeMilliseconds(), 102f, 96f, 99f, 98f, 2000, 20);

        // Act
        stocksResponse.UpdateLatestCandle(timeframe, nextCandle);

        // Assert
        stocksResponse.Results.Should().HaveCount(2);
        stocksResponse.Results.Last().Should().Be(nextCandle);
        stocksResponse.Results.First().Should().Be(lastCandle);
    }

    [Fact]
    public void UpdateLatestCandle_MinuteTimeframe_NewCandle_WhenTimestampExactlyAtCandleEnd_AddsNewCandle()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 1, 9, 30, 0, TimeSpan.Zero);
        var lastCandle = CreateBar(baseTime.ToUnixTimeMilliseconds(), 100f, 95f, 98f, 97f, 1000, 10);
        var stocksResponse = new StocksResponse
        {
            Results = new List<Bar> { lastCandle }
        };
        var timeframe = new Timeframe(5, Timespan.minute);
        var candleEnd = baseTime.AddMinutes(5);
        var nextCandle = CreateBar(candleEnd.ToUnixTimeMilliseconds(), 102f, 96f, 99f, 98f, 2000, 20);

        // Act
        stocksResponse.UpdateLatestCandle(timeframe, nextCandle);

        // Assert
        stocksResponse.Results.Should().HaveCount(2);
        stocksResponse.Results.Last().Should().Be(nextCandle);
    }

    #endregion

    #region Update Existing Candle Tests (Minute Timeframe)

    [Fact]
    public void UpdateLatestCandle_MinuteTimeframe_UpdatesExistingCandle_WhenTimestampBeforeCandleEnd()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 1, 9, 30, 0, TimeSpan.Zero);
        var lastCandle = CreateBar(baseTime.ToUnixTimeMilliseconds(), 100f, 95f, 98f, 97f, 1000, 10);
        var stocksResponse = new StocksResponse
        {
            Results = new List<Bar> { lastCandle }
        };
        var timeframe = new Timeframe(5, Timespan.minute);
        var nextCandle = CreateBar(baseTime.AddMinutes(2).ToUnixTimeMilliseconds(), 102f, 94f, 99f, 98f, 2000, 20);

        // Act
        stocksResponse.UpdateLatestCandle(timeframe, nextCandle);

        // Assert
        stocksResponse.Results.Should().HaveCount(1);
        stocksResponse.Results[0].High.Should().Be(102); // Updated from 100
        stocksResponse.Results[0].Low.Should().Be(94);  // Updated from 95
        stocksResponse.Results[0].Close.Should().Be(99); // Updated from 98
        stocksResponse.Results[0].Volume.Should().Be(3000); // 1000 + 2000
        stocksResponse.Results[0].TransactionCount.Should().Be(30); // 10 + 20
        stocksResponse.Results[0].Vwap.Should().BeApproximately((99f + 102f + 94f) / 3f, 0.01f);
    }

    [Fact]
    public void UpdateLatestCandle_MinuteTimeframe_DoesNotUpdateHigh_WhenNextCandleHighIsLower()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 1, 9, 30, 0, TimeSpan.Zero);
        var lastCandle = CreateBar(baseTime.ToUnixTimeMilliseconds(), 100f, 95f, 98f, 97f, 1000, 10);
        var stocksResponse = new StocksResponse
        {
            Results = new List<Bar> { lastCandle }
        };
        var timeframe = new Timeframe(5, Timespan.minute);
        var nextCandle = CreateBar(baseTime.AddMinutes(2).ToUnixTimeMilliseconds(), 99f, 94f, 97f, 98f, 2000, 20);

        // Act
        stocksResponse.UpdateLatestCandle(timeframe, nextCandle);

        // Assert
        stocksResponse.Results[0].High.Should().Be(100); // Not updated
        stocksResponse.Results[0].Low.Should().Be(94);   // Updated
    }

    [Fact]
    public void UpdateLatestCandle_MinuteTimeframe_DoesNotUpdateLow_WhenNextCandleLowIsHigher()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 1, 9, 30, 0, TimeSpan.Zero);
        var lastCandle = CreateBar(baseTime.ToUnixTimeMilliseconds(), 100f, 95f, 98f, 97f, 1000, 10);
        var stocksResponse = new StocksResponse
        {
            Results = new List<Bar> { lastCandle }
        };
        var timeframe = new Timeframe(5, Timespan.minute);
        var nextCandle = CreateBar(baseTime.AddMinutes(2).ToUnixTimeMilliseconds(), 101f, 96f, 99f, 98f, 2000, 20);

        // Act
        stocksResponse.UpdateLatestCandle(timeframe, nextCandle);

        // Assert
        stocksResponse.Results[0].High.Should().Be(101); // Updated
        stocksResponse.Results[0].Low.Should().Be(95);   // Not updated
    }

    #endregion

    #region Hour Timeframe Tests

    [Fact]
    public void UpdateLatestCandle_HourTimeframe_NewCandle_WhenTimestampAfterCandleEnd_AddsNewCandle()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 1, 9, 30, 0, TimeSpan.Zero);
        var lastCandle = CreateBar(baseTime.ToUnixTimeMilliseconds(), 100f, 95f, 98f, 97f, 1000, 10);
        var stocksResponse = new StocksResponse
        {
            Results = new List<Bar> { lastCandle }
        };
        var timeframe = new Timeframe(1, Timespan.hour);
        var nextCandle = CreateBar(baseTime.AddHours(1).ToUnixTimeMilliseconds(), 102f, 96f, 99f, 98f, 2000, 20);

        // Act
        stocksResponse.UpdateLatestCandle(timeframe, nextCandle);

        // Assert
        stocksResponse.Results.Should().HaveCount(2);
        stocksResponse.Results.Last().Should().Be(nextCandle);
    }

    [Fact]
    public void UpdateLatestCandle_HourTimeframe_UpdatesExistingCandle_WhenTimestampBeforeCandleEnd()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 1, 9, 30, 0, TimeSpan.Zero);
        var lastCandle = CreateBar(baseTime.ToUnixTimeMilliseconds(), 100f, 95f, 98f, 97f, 1000, 10);
        var stocksResponse = new StocksResponse
        {
            Results = new List<Bar> { lastCandle }
        };
        var timeframe = new Timeframe(1, Timespan.hour);
        var nextCandle = CreateBar(baseTime.AddMinutes(30).ToUnixTimeMilliseconds(), 102f, 94f, 99f, 98f, 2000, 20);

        // Act
        stocksResponse.UpdateLatestCandle(timeframe, nextCandle);

        // Assert
        stocksResponse.Results.Should().HaveCount(1);
        stocksResponse.Results[0].High.Should().Be(102);
        stocksResponse.Results[0].Low.Should().Be(94);
        stocksResponse.Results[0].Close.Should().Be(99);
        stocksResponse.Results[0].Volume.Should().Be(3000);
        stocksResponse.Results[0].TransactionCount.Should().Be(30);
    }

    [Fact]
    public void UpdateLatestCandle_MultiHourTimeframe_NewCandle_WhenTimestampAfterCandleEnd_AddsNewCandle()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 1, 9, 30, 0, TimeSpan.Zero);
        var lastCandle = CreateBar(baseTime.ToUnixTimeMilliseconds(), 100f, 95f, 98f, 97f, 1000, 10);
        var stocksResponse = new StocksResponse
        {
            Results = new List<Bar> { lastCandle }
        };
        var timeframe = new Timeframe(2, Timespan.hour);
        var nextCandle = CreateBar(baseTime.AddHours(2).ToUnixTimeMilliseconds(), 102f, 96f, 99f, 98f, 2000, 20);

        // Act
        stocksResponse.UpdateLatestCandle(timeframe, nextCandle);

        // Assert
        stocksResponse.Results.Should().HaveCount(2);
        stocksResponse.Results.Last().Should().Be(nextCandle);
    }

    [Fact]
    public void UpdateLatestCandle_MultiHourTimeframe_UpdatesExistingCandle_WhenTimestampBeforeCandleEnd()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 1, 9, 30, 0, TimeSpan.Zero);
        var lastCandle = CreateBar(baseTime.ToUnixTimeMilliseconds(), 100f, 95f, 98f, 97f, 1000, 10);
        var stocksResponse = new StocksResponse
        {
            Results = new List<Bar> { lastCandle }
        };
        var timeframe = new Timeframe(2, Timespan.hour);
        var nextCandle = CreateBar(baseTime.AddHours(1).ToUnixTimeMilliseconds(), 102f, 94f, 99f, 98f, 2000, 20);

        // Act
        stocksResponse.UpdateLatestCandle(timeframe, nextCandle);

        // Assert
        stocksResponse.Results.Should().HaveCount(1);
        stocksResponse.Results[0].High.Should().Be(102);
        stocksResponse.Results[0].Low.Should().Be(94);
        stocksResponse.Results[0].Close.Should().Be(99);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void UpdateLatestCandle_VWAP_IsRecalculatedCorrectly()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 1, 9, 30, 0, TimeSpan.Zero);
        var lastCandle = CreateBar(baseTime.ToUnixTimeMilliseconds(), 100f, 95f, 98f, 97f, 1000, 10);
        var stocksResponse = new StocksResponse
        {
            Results = new List<Bar> { lastCandle }
        };
        var timeframe = new Timeframe(5, Timespan.minute);
        var nextCandle = CreateBar(baseTime.AddMinutes(2).ToUnixTimeMilliseconds(), 105f, 90f, 100f, 98f, 2000, 20);

        // Act
        stocksResponse.UpdateLatestCandle(timeframe, nextCandle);

        // Assert
        var expectedVwap = (100f + 105f + 90f) / 3f; // (Close + High + Low) / 3
        stocksResponse.Results[0].Vwap.Should().BeApproximately(expectedVwap, 0.01f);
    }

    [Fact]
    public void UpdateLatestCandle_MultipleUpdates_AccumulatesVolumeAndTransactionCount()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 1, 9, 30, 0, TimeSpan.Zero);
        var lastCandle = CreateBar(baseTime.ToUnixTimeMilliseconds(), 100f, 95f, 98f, 97f, 1000, 10);
        var stocksResponse = new StocksResponse
        {
            Results = new List<Bar> { lastCandle }
        };
        var timeframe = new Timeframe(5, Timespan.minute);

        // Act - First update
        var nextCandle1 = CreateBar(baseTime.AddMinutes(1).ToUnixTimeMilliseconds(), 101f, 94f, 99f, 98f, 500, 5);
        stocksResponse.UpdateLatestCandle(timeframe, nextCandle1);

        // Act - Second update
        var nextCandle2 = CreateBar(baseTime.AddMinutes(2).ToUnixTimeMilliseconds(), 102f, 93f, 100f, 98f, 750, 8);
        stocksResponse.UpdateLatestCandle(timeframe, nextCandle2);

        // Assert
        stocksResponse.Results.Should().HaveCount(1);
        stocksResponse.Results[0].Volume.Should().Be(2250); // 1000 + 500 + 750
        stocksResponse.Results[0].TransactionCount.Should().Be(23); // 10 + 5 + 8
        stocksResponse.Results[0].High.Should().Be(102); // Highest of all
        stocksResponse.Results[0].Low.Should().Be(93);   // Lowest of all
        stocksResponse.Results[0].Close.Should().Be(100); // Last close
    }

    #endregion

    #region Exception Tests

    [Fact]
    public void UpdateLatestCandle_UnsupportedTimespan_ThrowsNotSupportedException()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 1, 9, 30, 0, TimeSpan.Zero);
        var lastCandle = CreateBar(baseTime.ToUnixTimeMilliseconds(), 100f, 95f, 98f, 97f, 1000, 10);
        var stocksResponse = new StocksResponse
        {
            Results = new List<Bar> { lastCandle }
        };
        // Assuming there's a day timespan that's not supported in the switch statement
        // We'll need to check what timespans exist, but for now let's test with an invalid one
        // Actually, looking at the code, it only supports minute and hour, so any other timespan should throw
        var timeframe = new Timeframe(1, Timespan.week);
        var nextCandle = CreateBar(baseTime.AddDays(1).ToUnixTimeMilliseconds(), 102f, 96f, 99f, 98f, 2000, 20);

        // Act & Assert
        var act = () => stocksResponse.UpdateLatestCandle(timeframe, nextCandle);
        act.Should().Throw<NotSupportedException>()
            .WithMessage($"Timespan {Timespan.week} is not supported.");
    }

    #endregion

    #region Helper Methods

    private static Bar CreateBar(long timestamp, float high, float low, float close, float open, int volume, int transactionCount)
    {
        return new Bar
        {
            Timestamp = timestamp,
            High = high,
            Low = low,
            Close = close,
            Open = open,
            Volume = volume,
            TransactionCount = transactionCount,
            Vwap = (close + high + low) / 3f
        };
    }

    #endregion
}
