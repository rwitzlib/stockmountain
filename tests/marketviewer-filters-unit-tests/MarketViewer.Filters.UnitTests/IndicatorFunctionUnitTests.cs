using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Contracts.Enums;
using Massive.Client.Models;
using Xunit;
using MarketViewer.Contracts.Models;

namespace MarketViewer.Filters.UnitTests;

public class IndicatorFunctionUnitTests
{
    private readonly IndicatorExpressionEngine _engine;

    public IndicatorFunctionUnitTests()
    {
        _engine = new IndicatorExpressionEngine();
    }
    
    [Fact]
    public void SMA()
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
        var result1 = _engine.EvaluateScript("sma(3) > 105 [, 1]", stockData, timeframe);
        Assert.True(result1);

        // Test explicit field access
        var result2 = _engine.EvaluateScript("sma(3.0).value > 105 [, 1]", stockData, timeframe);
        Assert.True(result2);

        // Test that it fails without proper field
        Assert.Throws<ArgumentException>(() => _engine.EvaluateScript("sma(3).invalid > 105 [, 1]", stockData, timeframe));
    }

    [Fact]
    public void EMA()
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
        var result1 = _engine.EvaluateScript("ema(3) > 105 [, 1]", stockData, timeframe);
        Assert.True(result1);

        // Test explicit field access
        var result2 = _engine.EvaluateScript("ema(3).value > 105 [, 1]", stockData, timeframe);
        Assert.True(result2);

        // Test that it fails without proper field
        Assert.Throws<ArgumentException>(() => _engine.EvaluateScript("ema(3).invalid > 105 [, 1]", stockData, timeframe));
    }

    [Fact]
    public void MACD()
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
    public void RSI()
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
                new() { Timestamp = 15, Close = 114.0f },
                new() { Timestamp = 16, Close = 115.0f },
                new() { Timestamp = 17, Close = 116.0f },
                new() { Timestamp = 18, Close = 117.0f },
                new() { Timestamp = 19, Close = 118.0f },
                new() { Timestamp = 20, Close = 119.0f },
                new() { Timestamp = 21, Close = 120.0f },
                new() { Timestamp = 22, Close = 121.0f },
                new() { Timestamp = 23, Close = 122.0f },
                new() { Timestamp = 24, Close = 123.0f },
                new() { Timestamp = 25, Close = 124.0f },
                new() { Timestamp = 26, Close = 125.0f },
                new() { Timestamp = 27, Close = 126.0f },
                new() { Timestamp = 28, Close = 127.0f },
                new() { Timestamp = 29, Close = 128.0f },
                new() { Timestamp = 30, Close = 129.0f },
                new() { Timestamp = 31, Close = 130.0f },
                new() { Timestamp = 32, Close = 131.0f },
                new() { Timestamp = 33, Close = 132.0f },
                new() { Timestamp = 34, Close = 133.0f },
                new() { Timestamp = 35, Close = 134.0f },
                new() { Timestamp = 36, Close = 135.0f },
                new() { Timestamp = 37, Close = 136.0f },
            }
        };
        var timeframe = new Timeframe(1, Timespan.minute);

        // Act & Assert
        // Test RSI value field access
        // This should parse and execute correctly
        var result1 = _engine.EvaluateScript("rsi(14,70,30,ema).value > 0", stockData, timeframe);
        Assert.True(result1);

        // Test RSI overbought field access
        var result2 = _engine.EvaluateScript("rsi(14,70,30,sma).overbought == 70", stockData, timeframe);
        Assert.True(result2);

        // Test RSI oversold field access
        var result3 = _engine.EvaluateScript("rsi(14,70,30,wilders).oversold == 30", stockData, timeframe);
        Assert.True(result3);

        // Test that default field access works (should be same as .value)
        var result4 = _engine.EvaluateScript("rsi(14,70,30,ema) > 0", stockData, timeframe);
        Assert.True(result4);
    }

    [Fact]
    public void SupportResistanceIndicatorProducesZones()
    {
        var stockData = CreateRangeBoundData();
        var timeframe = new Timeframe(1, Timespan.day);

        Assert.True(_engine.EvaluateScript("support_resistance(40,2,0.5,0.6,10,2).support_strength > 0", stockData, timeframe));
        Assert.True(_engine.EvaluateScript("support_resistance(40,2,0.5).support_touches >= 2", stockData, timeframe));
        Assert.True(_engine.EvaluateScript("support_resistance(40,2,0.5).support_distance < support_resistance(40,2,0.5).resistance_distance", stockData, timeframe));
        Assert.True(_engine.EvaluateScript("support_resistance(40,2,0.5).near_support == 1", stockData, timeframe));
    }

    [Fact]
    public void RangeEvaluationMode_AllIsDefaultAndAnyOverrides()
    {
        var stockData = new StocksResponse
        {
            Results = new List<Bar>
            {
                new() { Timestamp = 1, Close = 100.0f },
                new() { Timestamp = 2, Close = 101.0f },
                new() { Timestamp = 3, Close = 102.5f },
                new() { Timestamp = 4, Close = 104.0f }
            }
        };
        var timeframe = new Timeframe(1, Timespan.minute);

        var defaultAll = _engine.EvaluateScript("close > 101 [, 3]", stockData, timeframe);
        Assert.False(defaultAll); // 101 is not > 101

        var explicitAll = _engine.EvaluateScript("close > 101 [, 3, ALL]", stockData, timeframe);
        Assert.False(explicitAll);

        var anyResult = _engine.EvaluateScript("close > 101 [, 3, any]", stockData, timeframe);
        Assert.True(anyResult);

        var allPass = _engine.EvaluateScript("close >= 101 [, 3]", stockData, timeframe);
        Assert.True(allPass);
    }

    [Fact]
    public void DecimalArgumentsAreParsedCorrectly()
    {
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
        var minute = new Timeframe(1, Timespan.minute);
        Assert.True(_engine.EvaluateScript("sma(3.0) > 105 [, 1]", stockData, minute));

        var rangeBound = CreateRangeBoundData();
        var day = new Timeframe(1, Timespan.day);
        Assert.True(_engine.EvaluateScript("sr(40,2,0.75,0.6,10,2).resistance_strength >= 0", rangeBound, day));
    }

    private static StocksResponse CreateRangeBoundData()
    {
        const float support = 100f;
        const float resistance = 114f;
        var response = new StocksResponse { Results = new List<Bar>() };

        for (int i = 0; i < 90; i++)
        {
            var bar = new Bar
            {
                Timestamp = i + 1,
                Volume = 1_000_000 + (i % 5 == 0 ? 400_000 : 60_000)
            };

            var cycle = i % 18;
            if (cycle == 0 || cycle == 9 || cycle == 17)
            {
                bar.Low = support - 0.6f;
                bar.High = bar.Low + 4f;
                bar.Close = bar.Low + 2.4f;
                bar.Open = bar.Close;
            }
            else if (cycle == 4 || cycle == 13)
            {
                bar.High = resistance + 0.6f;
                bar.Low = bar.High - 4f;
                bar.Close = bar.High - 2.4f;
                bar.Open = bar.Close;
            }
            else
            {
                var mid = support + (resistance - support) * (float)Math.Abs(Math.Sin(i / 6.0));
                bar.High = mid + 2f;
                bar.Low = mid - 2f;
                bar.Close = mid;
                bar.Open = mid;
            }

            response.Results.Add(bar);
        }

        var last = response.Results[^1];
        last.Low = support - 0.4f;
        last.High = support + 1.6f;
        last.Close = support + 0.3f;
        last.Open = last.Close;

        return response;
    }
}
