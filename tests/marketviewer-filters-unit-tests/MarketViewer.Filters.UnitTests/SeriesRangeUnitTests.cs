using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Contracts.Enums;
using Massive.Client.Models;
using Xunit;
using MarketViewer.Contracts.Models;

namespace MarketViewer.Filters.UnitTests;

public class SeriesRangeUnitTests
{
    private readonly IndicatorExpressionEngine _engine = new();
    private static readonly Timeframe Minute = new(1, Timespan.minute);

    // closes 100..112 step 2 -> sma(3) has 5 values [102, 104, 106, 108, 110]; sma(2) has 6 [101..111].
    private static StocksResponse SevenBars() => new()
    {
        Results =
        [
            new Bar { Timestamp = 1, Close = 100.0f },
            new Bar { Timestamp = 2, Close = 102.0f },
            new Bar { Timestamp = 3, Close = 104.0f },
            new Bar { Timestamp = 4, Close = 106.0f },
            new Bar { Timestamp = 5, Close = 108.0f },
            new Bar { Timestamp = 6, Close = 110.0f },
            new Bar { Timestamp = 7, Close = 112.0f },
        ]
    };

    [Fact]
    public void TestSeriesRangeComparison()
    {
        var stockData = SevenBars();

        // last 3 SMA values [106, 108, 110] are all > 105
        Assert.True(_engine.EvaluateScript("sma(3) > 105 [1m, 3]", stockData, Minute));

        // last 5 SMA values include 102 and 104
        Assert.False(_engine.EvaluateScript("sma(3) > 105 [1m, 5]", stockData, Minute));

        // impossible condition
        Assert.False(_engine.EvaluateScript("sma(3) > 120 [1m, 3]", stockData, Minute));
    }

    [Fact]
    public void All_Requires_The_Full_Window()
    {
        var stockData = SevenBars();

        // sma(3) has exactly 5 values, all > 100: a 5-candle window is full …
        Assert.True(_engine.EvaluateScript("sma(3) > 100 [1m, 5]", stockData, Minute));
        Assert.True(_engine.EvaluateScript("sma(3) > 100 [1m, 5, all]", stockData, Minute));

        // … a 6-candle window is not, so "all" is false even though every available value passes (plan 20, decision 4)
        Assert.False(_engine.EvaluateScript("sma(3) > 100 [1m, 6]", stockData, Minute));
        Assert.False(_engine.EvaluateScript("sma(3) > 100 [1m, 6, all]", stockData, Minute));

        // "any" only needs one available value
        Assert.True(_engine.EvaluateScript("sma(3) > 100 [1m, 6, any]", stockData, Minute));
        Assert.True(_engine.EvaluateScript("sma(3) > 109 [1m, 6, any]", stockData, Minute));
        Assert.False(_engine.EvaluateScript("sma(3) > 120 [1m, 6, any]", stockData, Minute));
    }

    [Fact]
    public void All_Requires_The_Full_Window_For_Raw_Series_And_Scalar_Sides()
    {
        var stockData = SevenBars();

        // 7 closes, all > 99
        Assert.True(_engine.EvaluateScript("close > 99 [1m, 7]", stockData, Minute));
        Assert.False(_engine.EvaluateScript("close > 99 [1m, 8]", stockData, Minute));
        Assert.True(_engine.EvaluateScript("close > 99 [1m, 8, any]", stockData, Minute));

        // scalar on the left
        Assert.True(_engine.EvaluateScript("200 > close [1m, 7]", stockData, Minute));
        Assert.False(_engine.EvaluateScript("200 > close [1m, 8]", stockData, Minute));

        // dot-field series (List<double>) follow the same rule
        Assert.True(_engine.EvaluateScript("sma(3).value > 100 [1m, 5]", stockData, Minute));
        Assert.False(_engine.EvaluateScript("sma(3).value > 100 [1m, 6]", stockData, Minute));
    }

    [Fact]
    public void TestSeriesVsSeriesComparisons()
    {
        var stockData = SevenBars();

        // sma(2) > sma(3) on every aligned pair (5 pairs available)
        Assert.True(_engine.EvaluateScript("sma(2) > sma(3) [1m, 3]", stockData, Minute));
        Assert.True(_engine.EvaluateScript("sma(2) >= sma(3) [1m, 3]", stockData, Minute));
        Assert.False(_engine.EvaluateScript("sma(2) < sma(3) [1m, 3]", stockData, Minute));
        Assert.False(_engine.EvaluateScript("sma(2) <= sma(3) [1m, 3]", stockData, Minute));

        // full window: 5 aligned pairs exist, 6 do not
        Assert.True(_engine.EvaluateScript("sma(2) > sma(3) [1m, 5]", stockData, Minute));
        Assert.False(_engine.EvaluateScript("sma(2) > sma(3) [1m, 6]", stockData, Minute));
        Assert.True(_engine.EvaluateScript("sma(2) > sma(3) [1m, 6, any]", stockData, Minute));

        // mixed List<IIndicatorResult> vs List<double>
        Assert.True(_engine.EvaluateScript("sma(2) > sma(3).value [1m, 5]", stockData, Minute));
        Assert.False(_engine.EvaluateScript("sma(2) > sma(3).value [1m, 6]", stockData, Minute));
    }

    [Fact]
    public void Every_Comparison_Operator_Honours_The_Full_Window_Rule()
    {
        var stockData = SevenBars();

        foreach (var op in new[] { ">", ">=", "<", "<=", "=", "!=" })
        {
            // A window longer than the data is false under all for every operator, whatever the values are.
            Assert.False(_engine.EvaluateScript($"close {op} 106 [1m, 8]", stockData, Minute));
            Assert.False(_engine.EvaluateScript($"close {op} sma(3) [1m, 8]", stockData, Minute));
        }

        // and "any" over the same window still finds a match when one exists
        Assert.True(_engine.EvaluateScript("close = 106 [1m, 8, any]", stockData, Minute));
        Assert.True(_engine.EvaluateScript("close != 106 [1m, 8, any]", stockData, Minute));
        Assert.True(_engine.EvaluateScript("close <= 100 [1m, 8, any]", stockData, Minute));
    }

    [Fact]
    public void TestEqualityWithEpsilon()
    {
        var stockData = new StocksResponse { Results = [new Bar { Timestamp = 1, Close = 100.0f }] };

        // Scalars within epsilon should be equal
        Assert.True(_engine.EvaluateScript("100 = 100.0000000005", stockData, Minute));
        Assert.False(_engine.EvaluateScript("100 != 100.0000000005", stockData, Minute));
    }
}
