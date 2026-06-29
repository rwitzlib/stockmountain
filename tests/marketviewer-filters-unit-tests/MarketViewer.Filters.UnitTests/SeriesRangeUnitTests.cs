using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Contracts.Enums;
using Polygon.Client.Models;
using Xunit;
using MarketViewer.Contracts.Models;

namespace MarketViewer.Filters.UnitTests;

public class SeriesRangeUnitTests
{
    private readonly IndicatorExpressionEngine _engine;

    public SeriesRangeUnitTests()
    {
        _engine = new IndicatorExpressionEngine();
    }

    [Fact]
    public void TestSeriesRangeComparison()
    {
        // Arrange - create enough data for SMA(3) to produce a series
        var stockData = new StocksResponse
        {
            Results = new List<Bar>
            {
                new() { Timestamp = 1, Close = 100.0f },
                new() { Timestamp = 2, Close = 102.0f },
                new() { Timestamp = 3, Close = 104.0f },
                new() { Timestamp = 4, Close = 106.0f },
                new() { Timestamp = 5, Close = 108.0f },
                new() { Timestamp = 6, Close = 110.0f },
                new() { Timestamp = 7, Close = 112.0f }
            }
        };
        var timeframe = new Timeframe(1, Timespan.minute);

        // Act & Assert
        // SMA(3) with range 3 should check the last 3 SMA values
        // SMA values: [102, 104, 106, 108, 110] - last 3 are [106, 108, 110]
        // All are > 105, so should return true
        var result1 = _engine.EvaluateScript("sma(3) > 105 [, 3]", stockData, timeframe);
        Assert.True(result1);

        // Test with range that includes lower values
        // Last 5 SMA values: [102, 104, 106, 108, 110] - includes values <= 105
        var result2 = _engine.EvaluateScript("sma(3) > 105 [, 3]", stockData, timeframe);
        Assert.True(result2); // Should still be true because 106, 108, 110 > 105

        // Test with range that includes lower values
        // Last 5 SMA values: [102, 104, 106, 108, 110] - includes values <= 105
        var result3 = _engine.EvaluateScript("sma(3) > 105 [, 5]", stockData, timeframe);
        Assert.False(result3); // Should still be true because 106, 108, 110 > 105

        // Test with impossible condition
        var result4 = _engine.EvaluateScript("sma(3) > 120 [, 3]", stockData, timeframe);
        Assert.False(result4); // No SMA values > 120
    }

    [Fact]
    public void TestSeriesVsSeriesComparisons()
    {
        // Arrange
        var stockData = new StocksResponse
        {
            Results = new List<Bar>
            {
                new() { Timestamp = 1, Close = 100.0f },
                new() { Timestamp = 2, Close = 102.0f },
                new() { Timestamp = 3, Close = 104.0f },
                new() { Timestamp = 4, Close = 106.0f },
                new() { Timestamp = 5, Close = 108.0f },
                new() { Timestamp = 6, Close = 110.0f }
            }
        };
        var timeframe = new Timeframe(1, Timespan.minute);

        // Act & Assert: sma(2) vs sma(3)
        Assert.True(_engine.EvaluateScript("sma(2) > sma(3) [, 3]", stockData, timeframe));
        Assert.True(_engine.EvaluateScript("sma(2) >= sma(3) [, 3]", stockData, timeframe));
        Assert.False(_engine.EvaluateScript("sma(2) < sma(3) [, 3]", stockData, timeframe));
        Assert.False(_engine.EvaluateScript("sma(2) <= sma(3) [, 3]", stockData, timeframe));
    }

    [Fact]
    public void TestEqualityWithEpsilon()
    {
        var stockData = new StocksResponse { Results = new List<Bar> { new() { Timestamp = 1, Close = 100.0f } } };
        var timeframe = new Timeframe(1, Timespan.minute);

        // Scalars within epsilon should be equal
        Assert.True(_engine.EvaluateScript("100 = 100.0000000005", stockData, timeframe));
        Assert.False(_engine.EvaluateScript("100 != 100.0000000005", stockData, timeframe));
    }
}
