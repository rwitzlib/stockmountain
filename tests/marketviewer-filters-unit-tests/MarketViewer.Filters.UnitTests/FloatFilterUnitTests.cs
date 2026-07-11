using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market;
using Polygon.Client.Models;
using Xunit;

namespace MarketViewer.Filters.UnitTests;

public class FloatFilterUnitTests
{
    private readonly IndicatorExpressionEngine _engine = new();
    private readonly Timeframe _timeframe = new(1, Timespan.minute);

    [Fact]
    public void Float_GreaterThan_Passes_When_Above_Threshold()
    {
        var stockData = CreateStockData(floatValue: 250_000);

        var result = _engine.EvaluateScript("float > 100000", stockData, _timeframe);

        Assert.True(result);
    }

    [Fact]
    public void Float_GreaterThan_Fails_When_Below_Threshold()
    {
        var stockData = CreateStockData(floatValue: 50_000);

        var result = _engine.EvaluateScript("float > 100000", stockData, _timeframe);

        Assert.False(result);
    }

    [Fact]
    public void Float_Fails_When_TickerDetails_Missing()
    {
        var stockData = new StocksResponse
        {
            Results =
            [
                new Bar { Timestamp = 1, Close = 10f }
            ]
        };

        var result = _engine.EvaluateScript("float > 100000", stockData, _timeframe);

        Assert.False(result);
    }

    [Fact]
    public void Float_Works_With_Session_Incremental_Evaluation()
    {
        var stockData = CreateStockData(floatValue: 250_000);
        var session = _engine.Compile("float > 100000");

        var full = session.Evaluate(stockData, _timeframe);
        stockData.Results.Add(new Bar { Timestamp = 2, Close = 11f });
        var incremental = session.EvaluateIncremental(stockData, _timeframe);

        Assert.True(full);
        Assert.True(incremental);
    }

    [Theory]
    [InlineData("float >= 100000", 100_000, true)]
    [InlineData("float < 100000", 50_000, true)]
    [InlineData("float <= 100000", 100_000, true)]
    [InlineData("float = 100000", 100_000, true)]
    [InlineData("float != 100000", 50_000, true)]
    public void Float_Supports_Comparison_Operators(string script, long floatValue, bool expected)
    {
        var stockData = CreateStockData(floatValue);

        var result = _engine.EvaluateScript(script, stockData, _timeframe);

        Assert.Equal(expected, result);
    }

    private static StocksResponse CreateStockData(long floatValue) => new()
    {
        Results =
        [
            new Bar { Timestamp = 1, Close = 10f }
        ],
        TickerInfo = new StocksResponse.Information
        {
            TickerDetails = new TickerDetails
            {
                Ticker = "TEST",
                Float = floatValue
            }
        }
    };
}
