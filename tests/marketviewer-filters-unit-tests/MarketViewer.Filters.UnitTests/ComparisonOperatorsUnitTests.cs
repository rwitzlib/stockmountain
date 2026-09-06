using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Contracts.Enums;
using Massive.Client.Models;
using Xunit;
using MarketViewer.Contracts.Models;

namespace MarketViewer.Filters.UnitTests;

public class ComparisonOperatorsUnitTests
{
    private readonly IndicatorExpressionEngine _engine;

    public ComparisonOperatorsUnitTests()
    {
        _engine = new IndicatorExpressionEngine();
    }

    [Fact]
    public void TestSimpleComparison()
    {
        // Arrange
        var stockData = new StocksResponse
        {
            Results = new List<Bar>
            {
                new() { Timestamp = 1, Close = 100.0f },
                new() { Timestamp = 2, Close = 105.0f },
                new() { Timestamp = 3, Close = 102.0f }
            }
        };
        var timeframe = new Timeframe(1, Timespan.minute);

        // Act & Assert
        // This should work: 100 > 99
        var result1 = _engine.EvaluateScript("100 > 99", stockData, timeframe);
        Assert.True(result1);

        // This should fail: 100 > 101
        var result2 = _engine.EvaluateScript("100 > 101", stockData, timeframe);
        Assert.False(result2);
    }

    [Fact]
    public void TestEqualOperators()
    {
        // Arrange
        var stockData = new StocksResponse
        {
            Results = new List<Bar>
            {
                new() { Timestamp = 1, Close = 100.0f },
                new() { Timestamp = 2, Close = 105.0f },
                new() { Timestamp = 3, Close = 102.0f }
            }
        };
        var timeframe = new Timeframe(1, Timespan.minute);

        // Act & Assert
        var result1 = _engine.EvaluateScript("100 = 100", stockData, timeframe);
        Assert.True(result1);

        var result2 = _engine.EvaluateScript("100 == 100", stockData, timeframe);
        Assert.True(result2);

        var result3 = _engine.EvaluateScript("100 != 100", stockData, timeframe);
        Assert.False(result3);

        var result4 = _engine.EvaluateScript("100 != 101", stockData, timeframe);
        Assert.True(result4);
    }

    [Fact]
    public void TestSmaFunction()
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
        // SMA(3) of [100, 102, 104, 106, 108] = (104+106+108)/3 = 106
        // 106 > 105 should be true
        var result = _engine.EvaluateScript("sma(3) > 105", stockData, timeframe);
        Assert.True(result);
    }

    [Fact]
    public void TestComplexExpression()
    {
        // Arrange
        var stockData = new StocksResponse
        {
            Results = new List<Bar>
            {
                new() { Timestamp = 1, Close = 100.0f },
                new() { Timestamp = 2, Close = 105.0f },
                new() { Timestamp = 3, Close = 110.0f },
                new() { Timestamp = 4, Close = 115.0f },
                new() { Timestamp = 5, Close = 120.0f }
            }
        };
        var timeframe = new Timeframe(1, Timespan.minute);

        // Act & Assert
        // Test: sma(3) > 110 AND 120 > 115
        // SMA(3) = (110+115+120)/3 = 115, 115 > 110 is true
        // 120 > 115 is true
        // true AND true = true
        var result = _engine.EvaluateScript("sma(3) > 110 AND 120 > 115", stockData, timeframe);
        Assert.True(result);
    }

    [Fact]
    public void TestGreaterThanOrEqualOperator()
    {
        // Arrange
        var stockData = new StocksResponse
        {
            Results = new List<Bar>
            {
                new() { Timestamp = 1, Close = 100.0f },
                new() { Timestamp = 2, Close = 105.0f },
                new() { Timestamp = 3, Close = 102.0f }
            }
        };
        var timeframe = new Timeframe(1, Timespan.minute);

        // Act & Assert
        // Test >= operator: 100 >= 100 should be true
        var result1 = _engine.EvaluateScript("100 >= 100", stockData, timeframe);
        Assert.True(result1);

        // Test >= operator: 100 >= 99 should be true
        var result2 = _engine.EvaluateScript("100 >= 99", stockData, timeframe);
        Assert.True(result2);

        // Test >= operator: 100 >= 101 should be false
        var result3 = _engine.EvaluateScript("100 >= 101", stockData, timeframe);
        Assert.False(result3);
    }

    [Fact]
    public void TestLessThanOrEqualOperator()
    {
        // Arrange
        var stockData = new StocksResponse
        {
            Results = new List<Bar>
            {
                new() { Timestamp = 1, Close = 100.0f },
                new() { Timestamp = 2, Close = 105.0f },
                new() { Timestamp = 3, Close = 102.0f }
            }
        };
        var timeframe = new Timeframe(1, Timespan.minute);

        // Act & Assert
        // Test <= operator: 100 <= 100 should be true
        var result1 = _engine.EvaluateScript("100 <= 100", stockData, timeframe);
        Assert.True(result1);

        // Test <= operator: 99 <= 100 should be true
        var result2 = _engine.EvaluateScript("99 <= 100", stockData, timeframe);
        Assert.True(result2);

        // Test <= operator: 101 <= 100 should be false
        var result3 = _engine.EvaluateScript("101 <= 100", stockData, timeframe);
        Assert.False(result3);
    }

    [Fact]
    public void TestTimeframeRangeSyntax()
    {
        // Arrange
        var stockData = new StocksResponse
        {
            Results = new List<Bar>
            {
                new() { Timestamp = 1, Close = 100.0f },
                new() { Timestamp = 2, Close = 105.0f },
                new() { Timestamp = 3, Close = 110.0f },
                new() { Timestamp = 4, Close = 115.0f },
                new() { Timestamp = 5, Close = 120.0f }
            }
        };
        var timeframe = new Timeframe(1, Timespan.minute);

        // Act & Assert
        // Timeframe only
        Assert.True(_engine.EvaluateScript("close > 99 [5m]", stockData, timeframe));

        // Timeframe and candle window (all of the last 3 closes are > 99)
        Assert.True(_engine.EvaluateScript("close > 99 [5m, 3]", stockData, timeframe));
        Assert.True(_engine.EvaluateScript("close > 99 [1m, 3]", stockData, timeframe));
        Assert.True(_engine.EvaluateScript("close > 99 [1h, 2]", stockData, timeframe));

        // The suffix needs bar data to apply to: a literal-only line rejects it (plan 20, decision 2)
        Assert.ThrowsAny<Exception>(() => _engine.EvaluateScript("100 > 99 [5m]", stockData, timeframe));
        Assert.True(_engine.EvaluateScript("100 > 99", stockData, timeframe));

        var result6 = _engine.EvaluateScript("close < 110 [1h]", stockData, timeframe);
        Assert.False(result6);
    }
}
