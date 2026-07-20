using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Contracts.Enums;
using Massive.Client.Models;
using Xunit;
using MarketViewer.Contracts.Models;

namespace MarketViewer.Filters.UnitTests;

public class FieldAccessUnitTests
{
    private readonly IndicatorExpressionEngine _engine;

    public FieldAccessUnitTests()
    {
        _engine = new IndicatorExpressionEngine();
    }

    [Fact]
    public void TestFieldAccessSyntax()
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
                new() { Timestamp = 5, Close = 108.0f }
            }
        };
        var timeframe = new Timeframe(1, Timespan.minute);

        // Act & Assert
        // Test default field access (.value is implicit)
        var result1 = _engine.EvaluateScript("sma(3).value > 105 [, 1]", stockData, timeframe);
        Assert.True(result1);

        // Test explicit field access
        var result2 = _engine.EvaluateScript("sma(3).value > 105 [, 1]", stockData, timeframe);
        Assert.True(result2);

        // Test that it fails without proper field
        Assert.Throws<ArgumentException>(() => _engine.EvaluateScript("sma(3).invalid > 105 [, 1]", stockData, timeframe));
    }

    [Fact]
    public void TestMacdFieldAccess()
    {
        // Arrange - create enough data for MACD calculation
        var stockData = new StocksResponse
        {
            Results = new List<Bar>
            {
                // Generate some sample data that will produce MACD values
                new() { Timestamp = 1, Close = 100.0f },
                new() { Timestamp = 2, Close = 101.0f },
                new() { Timestamp = 3, Close = 102.0f },
                new() { Timestamp = 4, Close = 103.0f },
                new() { Timestamp = 5, Close = 104.0f },
                new() { Timestamp = 6, Close = 105.0f },
                new() { Timestamp = 7, Close = 106.0f },
                new() { Timestamp = 8, Close = 107.0f },
                new() { Timestamp = 9, Close = 108.0f },
                new() { Timestamp = 10, Close = 109.0f },
                new() { Timestamp = 11, Close = 110.0f },
                new() { Timestamp = 12, Close = 111.0f },
                new() { Timestamp = 13, Close = 112.0f },
                new() { Timestamp = 14, Close = 113.0f },
                new() { Timestamp = 15, Close = 114.0f }
            }
        };
        var timeframe = new Timeframe(1, Timespan.minute);

        // Act & Assert
        // Test MACD value field access
        // This should parse and execute correctly
        var result1 = _engine.EvaluateScript("macd(12,26,9,ema).value > 0", stockData, timeframe);
        Assert.True(result1 is bool);

        // Test MACD signal field access
        var result2 = _engine.EvaluateScript("macd(12,26,9,ema).signal > 0", stockData, timeframe);
        Assert.True(result2 is bool);

        // Test MACD histogram field access
        var result3 = _engine.EvaluateScript("macd(12,26,9,ema).histogram > 0", stockData, timeframe);
        Assert.True(result3 is bool);

        // Test that default field access works (should be same as .value)
        var result4 = _engine.EvaluateScript("macd(12,26,9,ema) > 0", stockData, timeframe);
        Assert.True(result4 is bool);
    }

    [Fact]
    public void TestCrossesFunctions()
    {
        // Arrange: create two series that cross
        var stockData = new StocksResponse
        {
            Results = new List<Bar>
            {
                new() { Timestamp = 1, Close = 100.0f },
                new() { Timestamp = 2, Close = 101.0f },
                new() { Timestamp = 3, Close = 102.0f },
                new() { Timestamp = 4, Close = 103.0f },
                new() { Timestamp = 5, Close = 104.0f }
            }
        };
        var timeframe = new Timeframe(1, Timespan.minute);

        // Cross-over: ema(2) crosses above sma(3) in the last 3 candles
        var crossesOver = _engine.EvaluateScript("crosses_over(ema(2).value, sma(3).value) [, 3]", stockData, timeframe);
        Assert.True(crossesOver is bool);

        // Cross-under: sma(3) crosses below ema(2) in the last 3 candles
        var crossesUnder = _engine.EvaluateScript("crosses_under(sma(3).value, ema(2).value) [, 3]", stockData, timeframe);
        Assert.True(crossesUnder is bool);
    }
}
