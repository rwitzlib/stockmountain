using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Contracts.Enums;
using Massive.Client.Models;
using Xunit;
using MarketViewer.Contracts.Models;

namespace MarketViewer.Filters.UnitTests;

public class ComparisonFunctionUnitTests
{
    private readonly IndicatorExpressionEngine _engine;

    public ComparisonFunctionUnitTests()
    {
        _engine = new IndicatorExpressionEngine();
    }

    [Fact]
    public void TestCrossesOverFunction()
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
        // Test crosses_above function with basic syntax
        var result1 = _engine.EvaluateScript("crosses_over(105, 100)", stockData, timeframe);

        // Test with timeframe syntax
        var result2 = _engine.EvaluateScript("crosses_over(105, 100) [5m, 3]", stockData, timeframe);

        var result3 = _engine.EvaluateScript("crosses_over(105, 100) [, 3]", stockData, timeframe);
    }
}
