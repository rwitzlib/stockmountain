using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Contracts.Enums;
using Polygon.Client.Models;
using Xunit;
using MarketViewer.Contracts.Models;

namespace MarketViewer.Filters.UnitTests;

public class TransformFunctionUnitTests
{
    private readonly IndicatorExpressionEngine _engine;

    public TransformFunctionUnitTests()
    {
        _engine = new IndicatorExpressionEngine();
    }

    [Fact]
    public void TestTransformFunctions()
    {
        // Arrange
        var stockData = new StocksResponse
        {
            Results = new List<Bar>
            {
                new() { Timestamp = 1, Close = 0f },
                new() { Timestamp = 1, Close = 1f },
                new() { Timestamp = 2, Close = 2f },
                new() { Timestamp = 3, Close = 3f },
                new() { Timestamp = 4, Close = 4f },
                new() { Timestamp = 5, Close = 5f }
            }
        };
        var timeframe = new Timeframe(1, Timespan.minute);

        // Act & Assert
        // Test close prices
        var closeResult = _engine.EvaluateScript("slope(close,5) > 0", stockData, timeframe);
        Assert.True(closeResult);

        var closeResult1 = _engine.EvaluateScript("slope(close,5) == 1", stockData, timeframe);
        Assert.True(closeResult1);

        var closeResult2 = _engine.EvaluateScript("slope(close,5) > 1", stockData, timeframe);
        Assert.False(closeResult2);

        var closeResult3 = _engine.EvaluateScript("slope(sma(3),3) > 0", stockData, timeframe);
        Assert.True(closeResult3);

        stockData.Results.Add(new Bar { Timestamp = 6, Close = 7f });

        var closeResult4 = _engine.EvaluateScript("slope(close,2) == 2", stockData, timeframe);
        Assert.True(closeResult4);
    }
}
