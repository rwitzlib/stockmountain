using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Filters.Expressions;
using MarketViewer.Filters.Parsing;
using Massive.Client.Models;
using Xunit;

namespace MarketViewer.Filters.UnitTests;

public class ParserGroupingUnitTests
{
    private readonly IndicatorExpressionEngine _engine = new();
    private static readonly Timeframe Tf = new(1, Timespan.minute);

    // close = 100, sma(2) = 99.5 -> "close > sma(2)" true; rsi-like conditions replaced by literals for clarity
    private static StocksResponse Data() => new()
    {
        Results = [new Bar { Timestamp = 1, Close = 99 }, new Bar { Timestamp = 2, Close = 100 }]
    };

    [Theory]
    [InlineData("close > 50 AND (close > 500 OR close > 90)", true)]      // a AND (b OR c): b false, c true
    [InlineData("close > 500 AND (close > 50 OR close > 90)", false)]     // a false
    [InlineData("(close > 500 OR close > 90) AND close > 50", true)]      // (b OR c) AND a
    [InlineData("close > 500 OR close > 90 AND close > 500", false)]      // flat fold: (false OR true) AND false
    [InlineData("close > 500 OR (close > 90 AND close > 500)", false)]    // grouped: false OR (true AND false)
    [InlineData("(close > 90 OR close > 500) AND close > 500", false)]
    [InlineData("NOT (close > 500 OR close > 90)", false)]                // NOT (false OR true)
    [InlineData("NOT (close > 500) OR close > 90", true)]                 // (NOT false) OR true
    [InlineData("((close > 90))", true)]
    [InlineData("(close) > (90)", true)]
    public void Grouping_Changes_Evaluation_As_Expected(string script, bool expected)
    {
        Assert.Equal(expected, _engine.EvaluateScript(script, Data(), Tf));
    }

    [Fact]
    public void Group_Builds_Right_Nested_Tree()
    {
        var expr = _engine.ParseExpression("close > 1 AND (close > 2 OR close > 3)");
        var and = Assert.IsType<BinaryExpression>(expr);
        Assert.Equal("AND", and.Operator.Symbol);
        var or = Assert.IsType<BinaryExpression>(and.Right);
        Assert.Equal("OR", or.Operator.Symbol);
    }

    [Fact]
    public void Group_Composes_With_Timeframe_Suffix()
    {
        var expr = _engine.ParseExpression("close > 1 AND (close > 2 OR close > 3) [5m, 3]");
        var range = Assert.IsType<TimeframeRangeExpression>(expr);
        Assert.Equal(5, range.GetTimeframe()!.Multiplier);
        Assert.Equal(3, range.GetRange());
        Assert.IsType<BinaryExpression>(range.GetInnerExpression());
    }

    [Theory]
    [InlineData("close > 1 AND (close > 2 OR close > 3", "closing parenthesis")]
    [InlineData("close > 1) AND close > 2", "Unexpected ')'")]
    [InlineData("(close > 1)) ", "Unexpected ')'")]
    [InlineData("close > 1 AND", "Unexpected end")]
    [InlineData("()", "Unexpected token")]
    public void Malformed_Groups_Are_Rejected(string script, string messageFragment)
    {
        var ex = Assert.ThrowsAny<Exception>(() => _engine.ParseExpression(script));
        Assert.Contains(messageFragment, ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
