using MarketViewer.Contracts.Enums;
using MarketViewer.Filters.Expressions;
using Xunit;

namespace MarketViewer.Filters.UnitTests;

/// <summary>
/// Plan 20, decisions 1 and 2: the line suffix is strictly <c>[timeframe, candles, mode]</c>, the
/// timeframe is required inside a bracket, and every deviation is an error instead of a silent
/// reinterpretation. Also the two line-shape rules: no suffix on a scalar-only line, no <c>all</c>
/// on a cross-only line.
/// </summary>
public class RangeSuffixParserTests
{
    private readonly IndicatorExpressionEngine _engine = new();

    [Theory]
    [InlineData("close > sma(20) [1m]", 1, Timespan.minute, null, null)]
    [InlineData("close > sma(20) [5m, 3]", 5, Timespan.minute, 3, null)]
    [InlineData("close > sma(20) [5m, 3, any]", 5, Timespan.minute, 3, RangeEvaluationMode.Any)]
    [InlineData("close > sma(20) [1h, 2, ALL]", 1, Timespan.hour, 2, RangeEvaluationMode.All)]
    [InlineData("close > sma(20) [1m,5,any]", 1, Timespan.minute, 5, RangeEvaluationMode.Any)]
    [InlineData("close > sma(20) [ 15m , 4 , all ]", 15, Timespan.minute, 4, RangeEvaluationMode.All)]
    [InlineData("close > sma(20) [d]", 1, Timespan.day, null, null)]
    [InlineData("close > sma(20) [1m, 1]", 1, Timespan.minute, 1, null)]
    [InlineData("close > sma(20) [2 hours]", 2, Timespan.hour, null, null)]
    public void Strict_Order_Parses_Into_Timeframe_Candles_Mode(string script, int multiplier, Timespan timespan, int? candles, RangeEvaluationMode? mode)
    {
        var range = Assert.IsType<TimeframeRangeExpression>(_engine.ParseExpression(script));

        Assert.Equal(multiplier, range.GetTimeframe()!.Multiplier);
        Assert.Equal(timespan, range.GetTimeframe()!.Timespan);
        Assert.Equal(candles, range.GetRange());
        Assert.Equal(mode, range.GetRangeEvaluationMode());
    }

    [Fact]
    public void Bare_Line_Has_No_Range_Wrapper()
    {
        Assert.IsType<BinaryExpression>(_engine.ParseExpression("close > sma(20)"));
    }

    [Theory]
    [InlineData("close > sma(20) [5]", "Timeframe is required")]
    [InlineData("close > sma(20) [, 5]", "Timeframe is required")]
    [InlineData("close > sma(20) [5, 1m]", "Timeframe is required")]
    [InlineData("close > sma(20) [any]", "goes last")]
    [InlineData("close > sma(20) [all]", "goes last")]
    [InlineData("close > sma(20) [any, 5, 1m]", "goes last")]
    [InlineData("close > sma(20) [1m, any]", "needs a candle count")]
    [InlineData("close > sma(20) [1m, all]", "needs a candle count")]
    [InlineData("close > sma(20) [1m, 1, any]", "only applies over more than one candle")]
    [InlineData("close > sma(20) [1m, 1, all]", "only applies over more than one candle")]
    [InlineData("close > sma(20) [1m, 5, 3]", "Expected 'any' or 'all'")]
    [InlineData("close > sma(20) [1m, 5, sometimes]", "Expected 'any' or 'all'")]
    [InlineData("close > sma(20) [1m, 5m]", "Expected a candle count")]
    [InlineData("close > sma(20) [1m, 5, any, x]", "Too many items")]
    [InlineData("close > sma(20) []", "Empty suffix")]
    [InlineData("close > sma(20) [1m, ]", "Empty candle count")]
    [InlineData("close > sma(20) [1m, 5, ]", "Empty mode")]
    [InlineData("close > sma(20) [1m, 0]", "at least 1")]
    [InlineData("close > sma(20) [1m, -2]", "at least 1")]
    [InlineData("close > sma(20) [1x]", "Unknown timeframe")]
    [InlineData("close > sma(20) [0m]", "Unknown timeframe")]
    [InlineData("close > sma(20) [1m] AND rsi(14) < 30 [5m]", "Only one")]
    [InlineData("close > sma(20) [1m] AND rsi(14) < 30", "must be the last thing")]
    [InlineData("close > sma(20) [1m", "Unbalanced bracket")]
    [InlineData("close > sma(20) 1m]", "Unbalanced bracket")]
    [InlineData("[1m]", "Expected an expression")]
    public void Malformed_Suffix_Is_Rejected_Not_Reinterpreted(string script, string messageFragment)
    {
        var ex = Assert.ThrowsAny<Exception>(() => _engine.ParseExpression(script));
        Assert.Contains(messageFragment, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("float > 1000000 [1m]")]
    [InlineData("float > 1000000 [1d, 5]")]
    [InlineData("100 > 99 [5m]")]
    [InlineData("float > 1000000 AND float < 5000000 [1m]")]
    [InlineData("NOT float > 1000000 [1m]")]
    public void Scalar_Only_Line_Rejects_Any_Suffix(string script)
    {
        var ex = Assert.ThrowsAny<Exception>(() => _engine.ParseExpression(script));
        Assert.Contains("does not apply to a line with no bar data", ex.Message);
    }

    [Theory]
    [InlineData("float > 1000000")]
    [InlineData("100 > 99")]
    [InlineData("float > 1000000 AND close > sma(20) [1m]")]
    [InlineData("float > 1000000 AND crosses_over(close, sma(20)) [1m, 3]")]
    [InlineData("time > 9:45 [1m]")]
    [InlineData("crosses_over(105, 100) [1m]")]
    public void Scalar_Rule_Allows_Bare_Scalar_Lines_And_Lines_With_Bar_Data(string script)
    {
        Assert.NotNull(_engine.ParseExpression(script));
    }

    [Theory]
    [InlineData("crosses_over(close, sma(20)) [1m, 5, all]")]
    [InlineData("crosses_under(close, sma(20)) [5m, 2, ALL]")]
    [InlineData("NOT crosses_over(close, sma(20)) [1m, 3, all]")]
    [InlineData("crosses_over(close, sma(20)) OR crosses_under(close, sma(50)) [1m, 3, all]")]
    public void Cross_Only_Line_Rejects_Explicit_All(string script)
    {
        var ex = Assert.ThrowsAny<Exception>(() => _engine.ParseExpression(script));
        Assert.Contains("'all' does not apply to a cross", ex.Message);
        Assert.Contains(", any]", ex.Message);
    }

    [Theory]
    [InlineData("crosses_over(close, sma(20)) [1m, 5, any]")]
    [InlineData("crosses_over(close, sma(20)) [1m, 5]")]
    [InlineData("crosses_over(close, sma(20)) [1m]")]
    [InlineData("close > sma(20) AND crosses_over(close, vwap()) [1m, 5, all]")]
    [InlineData("crosses_over(close, sma(20)) AND rsi(14) < 60 [15m, 3, all]")]
    public void Cross_Rule_Allows_Any_Default_And_Mixed_Lines(string script)
    {
        Assert.NotNull(_engine.ParseExpression(script));
    }

    [Fact]
    public void Shape_Helpers_Classify_Lines()
    {
        var scalar = _engine.ParseExpression("float > 1000000");
        Assert.True(ExpressionShape.IsScalarOnly(scalar));
        Assert.False(ExpressionShape.IsCrossOnly(scalar));

        var cross = _engine.ParseExpression("crosses_over(close, sma(20)) [1m, 3]");
        Assert.True(ExpressionShape.IsCrossOnly(cross));
        Assert.False(ExpressionShape.IsScalarOnly(cross));
        Assert.False(ExpressionShape.HasComparison(cross));

        var mixed = _engine.ParseExpression("close > sma(20) AND crosses_over(close, vwap()) [1m, 5, all]");
        Assert.False(ExpressionShape.IsCrossOnly(mixed));
        Assert.True(ExpressionShape.HasBooleanFunction(mixed));
        Assert.True(ExpressionShape.HasComparison(mixed));

        var series = _engine.ParseExpression("close > 100");
        Assert.False(ExpressionShape.IsScalarOnly(series));
        Assert.False(ExpressionShape.IsCrossOnly(series));
    }

    [Fact]
    public void Parse_Errors_Report_Token_Positions()
    {
        var ex = Assert.ThrowsAny<Exception>(() => _engine.ParseExpression("close > > 5"));
        Assert.Contains("'>' at position 8", ex.Message);

        ex = Assert.ThrowsAny<Exception>(() => _engine.ParseExpression("close > 5 ) AND close > 6"));
        Assert.Contains("Unexpected ')' at position 10", ex.Message);
    }
}
