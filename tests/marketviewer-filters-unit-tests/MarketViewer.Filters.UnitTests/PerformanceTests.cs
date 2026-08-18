using System.Diagnostics;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Filters.Functions.Indicators;
using MarketViewer.Filters.Functions.Transforms;
using MarketViewer.Filters.Interfaces;
using Massive.Client.Models;
using Xunit;

namespace MarketViewer.Filters.UnitTests;

/// <summary>
/// Guards the one property the golden/correctness tests cannot: that the incremental
/// (<see cref="IIncrementalSeriesFunction.Append"/> / <c>FilterSession.EvaluateIncremental</c>) path
/// does O(1) work per new bar instead of silently recomputing the whole series. A full recompute
/// inside <c>Append</c> is still *correct*, so only a scaling comparison catches it.
///
/// Wall-clock is inherently noisy (xUnit runs classes in parallel, GC pauses, loaded machines), so
/// these tests are hardened rather than exact: both paths are JIT-warmed untimed, timing uses
/// <see cref="Stopwatch.ElapsedTicks"/> rather than whole milliseconds, workloads are sized so the
/// full recompute is unmistakably quadratic (true ratio well over 10×), and the assertion only
/// requires a <see cref="MinSpeedup"/>× margin. Exclude with
/// <c>dotnet test --filter Category!=Performance</c> when a fully deterministic run is wanted.
/// (plans/14-golden-filter-tests.md follow-up #10)
/// </summary>
[Trait("Category", "Performance")]
public class PerformanceTests
{
    private const double MinSpeedup = 2.0;

    private static Bar MakeBar(int i) =>
        new() { Timestamp = i, Close = 100 + (float)(Math.Sin(i / 25.0) * 2) + i * 0.02f };

    private static StocksResponse MakeSeries(int count)
    {
        var stockData = new StocksResponse { Results = [] };
        for (int i = 0; i < count; i++)
        {
            stockData.Results.Add(MakeBar(i));
        }
        return stockData;
    }

    /// <summary>
    /// A path under test: given a fresh series, returns the per-bar step to run after each append.
    /// The full path recomputes from scratch each step; the incremental path primes once here and
    /// then appends.
    /// </summary>
    private delegate Action<StocksResponse> PathFactory(StocksResponse series);

    /// <summary>
    /// Runs both paths once per appended bar against fresh copies of the series (an untimed warm-up
    /// pass each, then a timed pass each) and asserts the incremental path is at least
    /// <see cref="MinSpeedup"/>× faster.
    /// </summary>
    private static void AssertIncrementalIsFaster(int initialBars, int steps, PathFactory full, PathFactory incremental, string what)
    {
        long Time(PathFactory factory)
        {
            var series = MakeSeries(initialBars);
            var step = factory(series);
            var sw = Stopwatch.StartNew();
            for (int s = 0; s < steps; s++)
            {
                series.Results.Add(MakeBar(initialBars + s));
                step(series);
            }
            sw.Stop();
            return sw.ElapsedTicks;
        }

        // Warm-up (JIT, caches) — untimed, both paths, so ordering doesn't bias the comparison.
        Time(full);
        Time(incremental);

        var fullTicks = Time(full);
        var incrTicks = Time(incremental);

        static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
        Assert.True(fullTicks > incrTicks * MinSpeedup,
            $"{what}: expected incremental to be ≥{MinSpeedup}× faster than full recompute over {steps} bars, " +
            $"but full={Ms(fullTicks):F1}ms vs incremental={Ms(incrTicks):F1}ms");
    }

    [Fact]
    public void Slope_Incremental_Is_Faster_Than_Full_Recompute()
    {
        var timeframe = new Timeframe(1, Timespan.minute);
        var slope = new SlopeFunction();
        var close = new Expressions.DataAccessExpression("close");
        const int period = 14;

        // The close series is a plain per-bar projection; both paths extend it by one point per
        // step so the measurement isolates SlopeFunction rather than series construction (which
        // would otherwise be O(n) on both sides and dilute the ratio to ~2×).
        (ExpressionContext ctx, List<IIndicatorResult> series) Prime(StocksResponse s)
        {
            var ctx = new ExpressionContext { StockData = s, Timeframe = timeframe };
            return (ctx, (List<IIndicatorResult>)close.Evaluate(ctx));
        }

        AssertIncrementalIsFaster(
            initialBars: 2000, steps: 2000,
            full: s =>
            {
                var (ctx, series) = Prime(s);
                return s2 =>
                {
                    series.Add(close.CreateBarResult(s2.Results[^1]));
                    slope.Execute([series, period], ctx);
                };
            },
            incremental: s =>
            {
                var (ctx, series) = Prime(s);
                var prev = (List<double>)slope.Execute([series, period], ctx);
                return s2 =>
                {
                    series.Add(close.CreateBarResult(s2.Results[^1]));
                    prev = (List<double>)((IIncrementalSeriesFunction)slope).Append([series, period], ctx, prev);
                };
            },
            what: "slope(close,14)");
    }

    [Fact]
    public void Rsi_Incremental_Is_Faster_Than_Full_Recompute()
    {
        var timeframe = new Timeframe(1, Timespan.minute);
        var rsi = new RsiFunction();
        object[] args = [14, 70.0, 30.0, "wilders"];

        AssertIncrementalIsFaster(
            initialBars: 2000, steps: 2000,
            full: s =>
            {
                var ctx = new ExpressionContext { StockData = s, Timeframe = timeframe };
                return _ => rsi.Execute(args, ctx);
            },
            incremental: s =>
            {
                var ctx = new ExpressionContext { StockData = s, Timeframe = timeframe };
                var prev = (List<IIndicatorResult>)rsi.Execute(args, ctx);
                return _ => prev = (List<IIndicatorResult>)((IIncrementalSeriesFunction)rsi).Append(args, ctx, prev);
            },
            what: "rsi(14,70,30,wilders)");
    }

    [Fact]
    public void Macd_Incremental_Is_Faster_Than_Full_Recompute()
    {
        var timeframe = new Timeframe(1, Timespan.minute);
        var macd = new MacdFunction();
        object[] args = [12, 26.0, 9.0, "ema"];

        AssertIncrementalIsFaster(
            initialBars: 2000, steps: 2000,
            full: s =>
            {
                var ctx = new ExpressionContext { StockData = s, Timeframe = timeframe };
                return _ => macd.Execute(args, ctx);
            },
            incremental: s =>
            {
                var ctx = new ExpressionContext { StockData = s, Timeframe = timeframe };
                var prev = (List<IIndicatorResult>)macd.Execute(args, ctx);
                return _ => prev = (List<IIndicatorResult>)((IIncrementalSeriesFunction)macd).Append(args, ctx, prev);
            },
            what: "macd(12,26,9,ema)");
    }

    [Fact]
    public void FilterSession_Incremental_Is_Faster_Than_Full_Recompute()
    {
        var timeframe = new Timeframe(1, Timespan.minute);
        var engine = new IndicatorExpressionEngine();
        const string script = "macd(12,26,9,ema).value > 0";
        var expression = engine.ParseExpression(script);

        AssertIncrementalIsFaster(
            initialBars: 2000, steps: 2000,
            full: _ => s => engine.EvaluateExpression(expression, s, timeframe),
            incremental: _ =>
            {
                var session = engine.Compile(script);
                return s => session.EvaluateIncremental(s, timeframe);
            },
            what: "FilterSession " + script);
    }
}
