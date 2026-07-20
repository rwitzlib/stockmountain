using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Contracts.Enums;
using Massive.Client.Models;
using Xunit;
using MarketViewer.Contracts.Models;

namespace MarketViewer.Filters.UnitTests;

public class LiteralPriceFunctionUnitTests
{
    private readonly IndicatorExpressionEngine _engine;

    public LiteralPriceFunctionUnitTests()
    {
        _engine = new IndicatorExpressionEngine();
    }

    [Fact]
    public void TestLiteralPriceFunctions()
    {
        // Arrange
        var stockData = new StocksResponse
        {
            Results = new List<Bar>
            {
                new() { Timestamp = 1, Close = 120.0f },
                new() { Timestamp = 2, Close = 118.0f },
                new() { Timestamp = 3, Close = 116.0f },
                new() { Timestamp = 4, Close = 114.0f },
                new() { Timestamp = 5, Close = 112.0f },
                new() { Timestamp = 6, Close = 110.0f, Open = 112.0f, High = 102.0f, Low = 98.0f, Vwap = 99.5f },
                new() { Timestamp = 7, Close = 108.0f, Open = 110.0f, High = 101.0f, Low = 98.0f, Vwap = 99.5f },
                new() { Timestamp = 8, Close = 100.0f, Open = 99.0f, High = 101.0f, Low = 98.0f, Vwap = 99.5f },
                new() { Timestamp = 9, Close = 102.0f, Open = 100.5f, High = 103.0f, Low = 99.5f, Vwap = 101.0f },
                new() { Timestamp = 10, Close = 108.0f, Open = 102.5f, High = 105.0f, Low = 101.5f, Vwap = 103.0f }
            }
        };
        var timeframe = new Timeframe(1, Timespan.minute);

        // Act & Assert
        // Test close prices
        var closeResult = _engine.EvaluateScript("close > 101 [, 2]", stockData, timeframe);
        Assert.True(closeResult);

        // Test open prices
        var openResult = _engine.EvaluateScript("open < 103 [, 2]", stockData, timeframe);
        Assert.True(openResult);

        // Test high prices
        var highResult = _engine.EvaluateScript("high > 102 [, 1]", stockData, timeframe);
        Assert.True(highResult);

        // Test low prices
        var lowResult = _engine.EvaluateScript("low < 102 [, 2]", stockData, timeframe);
        Assert.True(lowResult);

        // Test VWAP
        var vwapResult = _engine.EvaluateScript("vwap > 100 [, 2]", stockData, timeframe);
        Assert.True(vwapResult);

        // Test with Indicator Comarison
        var studyResult = _engine.EvaluateScript("sma(5) > 100 [ ,5]", stockData, timeframe);
        Assert.True(studyResult);

        // Test with comparison function
        var crossesResult = _engine.EvaluateScript("crosses_over(close, sma(5)) [, 3]", stockData, timeframe);
        Assert.True(crossesResult);
    }

    [Fact]
    public void TestPriceFunctionComparisons()
    {
        // Arrange
        var stockData = new StocksResponse
        {
            Results = new List<Bar>
            {
                new() { Timestamp = 1, Close = 100.0f, Open = 99.0f, High = 101.0f, Low = 98.0f, Vwap = 99.5f },
                new() { Timestamp = 2, Close = 102.0f, Open = 100.5f, High = 103.0f, Low = 99.5f, Vwap = 101.0f },
                new() { Timestamp = 3, Close = 104.0f, Open = 102.5f, High = 105.0f, Low = 101.5f, Vwap = 103.0f }
            }
        };
        var timeframe = new Timeframe(1, Timespan.minute);

        // Act & Assert
        // Close vs scalar
        Assert.True(_engine.EvaluateScript("close > 99 [, 1]", stockData, timeframe));

        // High vs low comparison
        Assert.True(_engine.EvaluateScript("high > low [, 1]", stockData, timeframe));

        // VWAP vs close
        Assert.True(_engine.EvaluateScript("close > vwap [, 1]", stockData, timeframe));
    }

    [Fact]
    public void TestAdvFunctionWithExplicitPeriod()
    {
        // Arrange
        var stockData = new StocksResponse
        {
            Results = new List<Bar>
            {
                new() { Timestamp = 1, Volume = 10 },
                new() { Timestamp = 2, Volume = 20 },
                new() { Timestamp = 3, Volume = 30 },
                new() { Timestamp = 4, Volume = 40 },
                new() { Timestamp = 5, Volume = 50 }
            }
        };
        var timeframe = new Timeframe(1, Timespan.minute);

        // Average volume = (10+20+30+40+50)/5 = 30
        var result = _engine.EvaluateScript("adv(5) > 25", stockData, timeframe);
        Assert.True(result);
    }

    [Fact]
    public void TestAdvFunctionDefaultPeriod()
    {
        // Arrange: provide 30 candles to satisfy default period requirement
        var bars = new List<Bar>();
        for (int i = 1; i <= 30; i++)
        {
            bars.Add(new Bar { Timestamp = i, Volume = 100 });
        }

        var stockData = new StocksResponse
        {
            Results = bars
        };
        var timeframe = new Timeframe(1, Timespan.minute);

        // Average volume = 100
        var result = _engine.EvaluateScript("adv() > 50", stockData, timeframe);
        Assert.True(result);
    }
}
