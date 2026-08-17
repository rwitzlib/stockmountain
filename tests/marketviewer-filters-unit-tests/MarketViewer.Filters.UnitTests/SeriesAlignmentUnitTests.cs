using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Filters.Expressions;
using MarketViewer.Filters.Interfaces;
using MarketViewer.Filters.Operators.Comparison;
using MarketViewer.Filters.Sessions;
using Massive.Client.Models;
using Xunit;

namespace MarketViewer.Filters.UnitTests;

/// <summary>
/// Plan 14 follow-up 5: comparisons pair series by position from the end, so every timestamped
/// series must be right-aligned with the context bars for the tail the comparison touches.
/// <see cref="SeriesAlignment"/> enforces that at the producer on both evaluation paths.
/// </summary>
public class SeriesAlignmentUnitTests
{
    private static readonly Timeframe Tf = new(1, Timespan.minute);

    private static StocksResponse Bars(int count) => new()
    {
        Results = Enumerable.Range(1, count).Select(i => new Bar { Timestamp = i * 60_000L, Close = 100 + i }).ToList()
    };

    private static ExpressionContext Ctx(StocksResponse data, int? range = null) => new()
    {
        StockData = data,
        Timeframe = Tf,
        CandleRange = range
    };

    private static List<IIndicatorResult> Points(params long[] timestamps) =>
        timestamps.Select(t => (IIndicatorResult)new SimpleIndicatorResult { Timestamp = t, Value = 1 }).ToList();

    /// <summary>A series function whose output the test controls entirely.</summary>
    private sealed class StubSeriesFunction(string name, Func<ExpressionContext, List<IIndicatorResult>> produce) : ISeriesFunction
    {
        public string Name => name;
        public object Execute(object[] parameters, ExpressionContext context) => produce(context);
    }

    // ---- the helper itself

    [Fact]
    public void Aligned_Tail_Passes_Even_When_Series_Starts_Late()
    {
        var data = Bars(10);
        // warm-up: series covers only the last 4 bars — legal
        var series = Points(7 * 60_000L, 8 * 60_000L, 9 * 60_000L, 10 * 60_000L);

        SeriesAlignment.AssertTail(series, Ctx(data), "stub");
        SeriesAlignment.AssertTail(series, Ctx(data, range: 4), "stub");
        // range longer than the series: only the overlapping tail is checked
        SeriesAlignment.AssertTail(series, Ctx(data, range: 8), "stub");
    }

    [Fact]
    public void Empty_Series_And_Empty_Bars_Are_Ignored()
    {
        SeriesAlignment.AssertTail([], Ctx(Bars(5)), "stub");
        SeriesAlignment.AssertTail(Points(60_000L), Ctx(new StocksResponse { Results = [] }), "stub");
    }

    [Fact]
    public void Series_That_Stops_Early_Throws()
    {
        var data = Bars(10);
        var stopsEarly = Points(7 * 60_000L, 8 * 60_000L, 9 * 60_000L); // missing the last bar

        var ex = Assert.Throws<InvalidOperationException>(() => SeriesAlignment.AssertTail(stopsEarly, Ctx(data), "stub"));
        Assert.Contains("'stub'", ex.Message);
        Assert.Contains("not aligned", ex.Message);
    }

    [Fact]
    public void Interior_Gap_Is_Caught_Only_When_Inside_The_Compared_Range()
    {
        var data = Bars(10);
        // bar 9 is missing: last point is fine, second-from-last is not
        var gap = Points(6 * 60_000L, 7 * 60_000L, 8 * 60_000L, 10 * 60_000L);

        SeriesAlignment.AssertTail(gap, Ctx(data), "stub");                       // range 1: only the last point is compared
        SeriesAlignment.AssertTail(gap, Ctx(data, range: 1), "stub");
        Assert.Throws<InvalidOperationException>(() => SeriesAlignment.AssertTail(gap, Ctx(data, range: 2), "stub"));
        Assert.Throws<InvalidOperationException>(() => SeriesAlignment.AssertTail(gap, Ctx(data, range: 5), "stub"));
    }

    // ---- direct path: IExpression.Evaluate

    [Fact]
    public void Direct_Path_Throws_When_A_Function_Emits_A_Misaligned_Series()
    {
        var data = Bars(6);
        var stale = new StubSeriesFunction("stale", ctx => Points(1 * 60_000L, 2 * 60_000L, 3 * 60_000L)); // stops at bar 3
        var expr = new BinaryExpression(new DataAccessExpression("close"), new GreaterThanOperator(), new FunctionCallExpression(stale, []));

        var ex = Assert.Throws<InvalidOperationException>(() => expr.Evaluate(Ctx(data)));
        Assert.Contains("'stale'", ex.Message);
    }

    [Fact]
    public void Direct_Path_Passes_For_An_Aligned_Function_And_For_Data_Access()
    {
        var data = Bars(6);
        var aligned = new StubSeriesFunction("aligned", ctx => Points(4 * 60_000L, 5 * 60_000L, 6 * 60_000L));
        var expr = new BinaryExpression(new DataAccessExpression("close"), new GreaterThanOperator(), new FunctionCallExpression(aligned, []));

        Assert.True((bool)expr.Evaluate(Ctx(data, range: 3)));
    }

    // ---- compiled path: FilterSession, full and incremental

    [Fact]
    public void Compiled_Path_Throws_On_Misaligned_Function_Full_And_Incremental()
    {
        var stale = new StubSeriesFunction("stale", ctx => Points(1 * 60_000L, 2 * 60_000L)); // never reaches the last bar
        var expr = new BinaryExpression(new FunctionCallExpression(stale, []), new GreaterThanOperator(), new LiteralExpression(0.0));
        var session = new FilterSession(expr);

        Assert.Throws<InvalidOperationException>(() => session.Evaluate(Bars(4), Tf));
        session.Reset();
        Assert.Throws<InvalidOperationException>(() => session.EvaluateIncremental(Bars(4), Tf));
    }

    [Fact]
    public void Compiled_Path_Real_Indicators_Stay_Aligned_Through_Incremental_Growth()
    {
        // sma/ema/rsi/close/.field: grow the bars one at a time and evaluate incrementally with a
        // range — every producer must satisfy the invariant on every step or this throws.
        var engine = new IndicatorExpressionEngine();
        var session = engine.Compile("close > sma(3) AND ema(3) > 0 OR rsi(3,70,30,wilders) > 0 OR macd(3,5,2,ema).histogram > -1000 [1m, 3]");
        var bars = new List<Bar>();
        for (int i = 1; i <= 20; i++)
        {
            bars.Add(new Bar { Timestamp = i * 60_000L, Close = 100 + (i % 4), High = 101 + (i % 4), Low = 99 + (i % 4), Volume = 1000 });
            var data = new StocksResponse { Results = bars };
            session.EvaluateIncremental(data, Tf);
            // forming-bar mutation of the last bar must keep alignment too
            bars[^1].Close += 0.5f;
            session.EvaluateIncremental(data, Tf);
        }
    }

    [Fact]
    public void Time_Field_Is_Exempt_From_Alignment()
    {
        // "time" is a single point stamped with the evaluation clock, which need not equal a bar.
        var engine = new IndicatorExpressionEngine();
        var data = Bars(5);
        var clock = DateTimeOffset.FromUnixTimeMilliseconds(99 * 60_000L);
        Assert.False(engine.EvaluateScript("time < 0:01", data, Tf, evaluationTime: clock));
        Assert.True(engine.EvaluateScript("time >= 0:00", data, Tf, evaluationTime: clock));
    }
}
