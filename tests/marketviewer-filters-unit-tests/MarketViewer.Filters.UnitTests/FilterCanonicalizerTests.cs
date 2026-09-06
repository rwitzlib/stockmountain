using MarketViewer.Contracts.Enums;
using MarketViewer.Filters.Expressions;
using MarketViewer.Filters.Interfaces;
using MarketViewer.Filters.Parsing;
using Xunit;

namespace MarketViewer.Filters.UnitTests;

/// <summary>
/// Plan 20, decisions 3 and 5: the canonical printer's rules, the parse → print → parse round trip,
/// and the display spans the UI splices into.
/// </summary>
public class FilterCanonicalizerTests
{
    private readonly IndicatorExpressionEngine _engine = new();

    public static IEnumerable<object[]> CanonicalCases() =>
    [
        // series lines always carry an explicit timeframe
        ["close > sma(20)", "close > sma(20) [1m]"],
        ["close>sma(20)[5m]", "close > sma(20) [5m]"],
        ["time >= 9:45 AND time < 11:00", "time >= 9:45 AND time < 11:00 [1m]"],
        ["volume > 50000 [1d]", "volume > 50000 [1d]"],
        // mode is written whenever more than one candle is examined
        ["close > sma(20) [5m, 3]", "close > sma(20) [5m, 3, all]"],
        ["close > sma(20) [5m, 3, ALL]", "close > sma(20) [5m, 3, all]"],
        ["rsi(14,70,30,wilders) > 70 [1m, 5, any]", "rsi(14, 70, 30, wilders) > 70 [1m, 5, any]"],
        // a single-candle window is the default and is dropped
        ["close > 100 [1m, 1]", "close > 100 [1m]"],
        // cross-only lines print any
        ["crosses_over(close, sma(20)) [1m, 5]", "crosses_over(close, sma(20)) [1m, 5, any]"],
        ["crosses_over(close,sma(20))", "crosses_over(close, sma(20)) [1m]"],
        ["close > sma(20) AND crosses_over(close, vwap()) [1m, 5]", "close > sma(20) AND crosses_over(close, vwap()) [1m, 5, all]"],
        // scalar-only lines stay bare
        ["float < 50000000", "float < 50000000"],
        ["float<50000000 AND float>1000000", "float < 50000000 AND float > 1000000"],
        // operators, keywords, aliases, fields, numbers
        ["close == open [h]", "close = open [1h]"],
        ["Close > SMA(20) [1M]", "close > sma(20) [1m]"],
        ["sr().near_support > 0 [1d]", "support_resistance().near_support > 0 [1d]"],
        ["macd(12,26,9,ema).Histogram > 0 [5m]", "macd(12, 26, 9, ema).histogram > 0 [5m]"],
        ["time.hour >= 10", "time.hour >= 10 [1m]"],
        ["close > 100.50", "close > 100.5 [1m]"],
        ["volume > 1e6 [1d]", "volume > 1000000 [1d]"],
        ["close > 1 AND adv(20) > 2000000 [1d]", "close > 1 AND adv(20) > 2000000 [1d]"],
        ["slope(sma(20),10) > 0", "slope(sma(20), 10) > 0 [1m]"],
        ["30 > rsi(14,70,30,wilders)", "30 > rsi(14, 70, 30, wilders) [1m]"],
        // logical grouping: different operator keeps parentheses, same-operator chains stay flat,
        // a right-nested same-operator group keeps its parentheses so the tree round-trips exactly
        ["close > sma(50) and (rsi(14) < 30 or rsi(14) > 70) [1d]", "close > sma(50) AND (rsi(14) < 30 OR rsi(14) > 70) [1d]"],
        ["close > 1 OR close > 2 AND close > 3", "close > 1 OR (close > 2 AND close > 3) [1m]"],
        ["(close > 1 OR close > 2) AND close > 3", "(close > 1 OR close > 2) AND close > 3 [1m]"],
        ["close > 1 AND close > 2 AND close > 3", "close > 1 AND close > 2 AND close > 3 [1m]"],
        ["close > 1 AND (close > 2 AND close > 3)", "close > 1 AND (close > 2 AND close > 3) [1m]"],
        ["NOT (close > 1 OR close > 2)", "NOT (close > 1 OR close > 2) [1m]"],
        ["not close > sma(20)", "NOT close > sma(20) [1m]"],
        ["NOT close > sma(20) AND rsi(14) < 50", "NOT close > sma(20) AND rsi(14) < 50 [1m]"],
        ["((close > 90))", "close > 90 [1m]"],
        ["(close) > (90)", "close > 90 [1m]"],
    ];

    [Theory]
    [MemberData(nameof(CanonicalCases))]
    public void Prints_Canonical_Form(string input, string expected)
    {
        Assert.Equal(expected, _engine.Canonicalize(input).Text);
    }

    [Theory]
    [MemberData(nameof(CanonicalCases))]
    public void Canonical_Text_Is_A_Fixpoint_And_Reparses_To_The_Same_Tree(string input, string expected)
    {
        var canonical = _engine.Canonicalize(input);
        var again = _engine.Canonicalize(canonical.Text);

        Assert.Equal(expected, again.Text);
        AssertSameTree(canonical.Root, again.Root);
        AssertSameTree(canonical.Root, _engine.ParseExpression(canonical.Text));
    }

    [Fact]
    public void Segments_Cover_The_Tokens_The_User_Can_Edit()
    {
        var canonical = _engine.Canonicalize("rsi(14,70,30,wilders) > 70 [1m, 5, any]");

        var pieces = canonical.Segments.Select(s => (s.Role, canonical.Slice(s), s.Edit)).ToList();
        Assert.Equal(
        [
            ("function", "rsi(14, 70, 30, wilders)", null),
            ("op", ">", "op"),
            ("literal", "70", "value"),
            ("timeframe", "1m", "timeframe"),
            ("timeframe", "5", "candles"),
            ("timeframe", "any", "mode"),
        ], pieces);
    }

    [Fact]
    public void Segments_For_Logic_And_Groups_Skip_Punctuation()
    {
        var canonical = _engine.Canonicalize("close > sma(50) AND (rsi(14) < 30 OR rsi(14) > 70) [1d]");

        Assert.Equal("close > sma(50) AND (rsi(14) < 30 OR rsi(14) > 70) [1d]", canonical.Text);
        var pieces = canonical.Segments.Select(s => (s.Role, canonical.Slice(s))).ToList();
        Assert.Equal(
        [
            ("data", "close"), ("op", ">"), ("function", "sma(50)"),
            ("logic", "AND"),
            ("function", "rsi(14)"), ("op", "<"), ("literal", "30"),
            ("logic", "OR"),
            ("function", "rsi(14)"), ("op", ">"), ("literal", "70"),
            ("timeframe", "1d"),
        ], pieces);
    }

    [Theory]
    [MemberData(nameof(CanonicalCases))]
    public void Segments_Are_Ordered_Disjoint_And_Inside_The_Text(string input, string _)
    {
        var canonical = _engine.Canonicalize(input);
        var previousEnd = 0;
        foreach (var segment in canonical.Segments)
        {
            Assert.True(segment.Start >= previousEnd, $"{input}: segment '{canonical.Slice(segment)}' overlaps the previous one");
            Assert.True(segment.End <= canonical.Text.Length);
            Assert.True(segment.End > segment.Start);
            Assert.False(string.IsNullOrWhiteSpace(canonical.Slice(segment)));
            previousEnd = segment.End;
        }
    }

    [Fact]
    public void A_Chip_Edit_Is_A_Splice_On_A_Span_Followed_By_Recanonicalization()
    {
        var canonical = _engine.Canonicalize("rsi(14,70,30,wilders) > 70 [1m, 5, any]");

        var mode = canonical.Segments.Single(s => s.Edit == "mode");
        var spliced = canonical.Text[..mode.Start] + "all" + canonical.Text[mode.End..];
        Assert.Equal("rsi(14, 70, 30, wilders) > 70 [1m, 5, all]", _engine.Canonicalize(spliced).Text);

        var timeframe = canonical.Segments.Single(s => s.Edit == "timeframe");
        spliced = canonical.Text[..timeframe.Start] + "5m" + canonical.Text[timeframe.End..];
        Assert.Equal("rsi(14, 70, 30, wilders) > 70 [5m, 5, any]", _engine.Canonicalize(spliced).Text);

        var candles = canonical.Segments.Single(s => s.Edit == "candles");
        spliced = canonical.Text[..candles.Start] + "1" + canonical.Text[candles.End..];
        // Dropping to one candle makes the mode meaningless: the strict parser says so rather than guessing.
        Assert.ThrowsAny<Exception>(() => _engine.Canonicalize(spliced));

        var op = canonical.Segments.Single(s => s.Edit == "op");
        spliced = canonical.Text[..op.Start] + "<=" + canonical.Text[op.End..];
        Assert.Equal("rsi(14, 70, 30, wilders) <= 70 [1m, 5, any]", _engine.Canonicalize(spliced).Text);
    }

    [Fact]
    public void Exposes_Suffix_Metadata()
    {
        var bare = _engine.Canonicalize("close > sma(20)");
        Assert.Equal(1, bare.Timeframe!.Multiplier);
        Assert.Equal(Timespan.minute, bare.Timeframe.Timespan);
        Assert.Null(bare.Candles);
        Assert.Null(bare.Mode);
        Assert.False(bare.IsScalarOnly);

        var window = _engine.Canonicalize("close > sma(20) [5m, 3]");
        Assert.Equal(3, window.Candles);
        Assert.Equal(RangeEvaluationMode.All, window.Mode);

        var cross = _engine.Canonicalize("crosses_over(close, sma(20)) [1m, 5]");
        Assert.Equal(RangeEvaluationMode.Any, cross.Mode);
        Assert.True(cross.IsCrossOnly);
        Assert.True(cross.HasCross);
        Assert.False(cross.HasComparison);

        var scalar = _engine.Canonicalize("float < 50000000");
        Assert.True(scalar.IsScalarOnly);
        Assert.Null(scalar.Timeframe);
        Assert.Empty(scalar.Segments.Where(s => s.Role == "timeframe"));

        var single = _engine.Canonicalize("close > 100 [1m, 1]");
        Assert.Null(single.Candles);
    }

    [Fact]
    public void Canonical_Root_Evaluates_Like_The_Original()
    {
        var data = new Contracts.Responses.Market.StocksResponse
        {
            Results =
            [
                new Massive.Client.Models.Bar { Timestamp = 1, Close = 100 },
                new Massive.Client.Models.Bar { Timestamp = 2, Close = 101 },
                new Massive.Client.Models.Bar { Timestamp = 3, Close = 102 },
                new Massive.Client.Models.Bar { Timestamp = 4, Close = 103 },
            ]
        };
        var timeframe = new Contracts.Models.Timeframe(1, Timespan.minute);

        foreach (var script in new[] { "close > 101 [1m, 3, any]", "close > 100 [1m, 3]", "close > 101 [1m, 3]", "close>100" })
        {
            var canonical = _engine.Canonicalize(script);
            Assert.Equal(
                _engine.EvaluateScript(script, data, timeframe),
                _engine.EvaluateExpression(canonical.Root, data, timeframe));
            Assert.Equal(
                _engine.EvaluateScript(script, data, timeframe),
                _engine.EvaluateScript(canonical.Text, data, timeframe));
        }
    }

    // ------------------------------------------------------------------ helpers

    private static void AssertSameTree(IExpression expected, IExpression actual)
    {
        switch (expected)
        {
            case TimeframeRangeExpression e:
                var a = Assert.IsType<TimeframeRangeExpression>(actual);
                Assert.Equal(e.GetTimeframe()?.Multiplier, a.GetTimeframe()?.Multiplier);
                Assert.Equal(e.GetTimeframe()?.Timespan, a.GetTimeframe()?.Timespan);
                Assert.Equal(e.GetRange(), a.GetRange());
                Assert.Equal(e.GetRangeEvaluationMode(), a.GetRangeEvaluationMode());
                AssertSameTree(e.GetInnerExpression(), a.GetInnerExpression());
                return;
            case BinaryExpression e:
                var b = Assert.IsType<BinaryExpression>(actual);
                Assert.Equal(e.Operator.Symbol, b.Operator.Symbol);
                AssertSameTree(e.Left, b.Left);
                AssertSameTree(e.Right, b.Right);
                return;
            case UnaryExpression e:
                var u = Assert.IsType<UnaryExpression>(actual);
                Assert.Equal(e.Operator.Symbol, u.Operator.Symbol);
                AssertSameTree(e.Operand, u.Operand);
                return;
            case FunctionCallExpression e:
                var f = Assert.IsType<FunctionCallExpression>(actual);
                Assert.Equal(e.FunctionName, f.FunctionName);
                Assert.Equal(e.GetArguments().Count, f.GetArguments().Count);
                for (var i = 0; i < e.GetArguments().Count; i++)
                    AssertSameTree(e.GetArguments()[i], f.GetArguments()[i]);
                return;
            case FieldAccessExpression e:
                var fa = Assert.IsType<FieldAccessExpression>(actual);
                Assert.Equal(e.GetFieldName(), fa.GetFieldName(), ignoreCase: true);
                AssertSameTree(e.GetTargetExpression(), fa.GetTargetExpression());
                return;
            case DataAccessExpression e:
                Assert.Equal(e.GetFieldName(), Assert.IsType<DataAccessExpression>(actual).GetFieldName());
                return;
            case LiteralExpression e:
                Assert.Equal(e.GetValue(), Assert.IsType<LiteralExpression>(actual).GetValue());
                return;
            default:
                Assert.Fail($"unexpected node {expected.GetType().Name}");
                return;
        }
    }
}
