using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Filters.Interfaces;
using Xunit;

namespace MarketViewer.Filters.UnitTests.Golden;

/// <summary>
/// Layer 1 of plans/14-golden-filter-tests.md: every indicator series the DSL can produce is
/// compared bar-by-bar against an independently computed reference on real Massive data.
/// Three assertions per (fixture, series): warm-up length, values within tolerance, and
/// incremental evaluation == full evaluation.
/// </summary>
public class GoldenIndicatorTests
{
    private static readonly IndicatorExpressionEngine Engine = new();

    // Relative tolerances (§1c). Recursive indicators accumulate float32-input rounding.
    private const double SimpleRelTol = 1e-4;     // sma, adv, slope of raw price
    private const double RecursiveRelTol = 1e-3;  // ema, macd, rsi and anything derived from them
    private const double AbsFloor = 1e-6;

    public static IEnumerable<object[]> Cases()
    {
        foreach (var name in GoldenFixture.Names())
        {
            foreach (var key in GoldenFixture.Load(name).Reference.Series.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                yield return [name, key];
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Series_Matches_Reference(string fixtureName, string key)
    {
        var fixture = GoldenFixture.Load(fixtureName);
        var expected = fixture.Reference.Series[key];
        var timeframe = TimeframeFor(fixtureName);

        var actual = Engine.EvaluateSeries(key, fixture.Bars, timeframe);

        Assert.Equal(expected.Length, actual.Length);

        // 1. Warm-up length: first index with a value must agree exactly.
        var expectedFirst = Array.FindIndex(expected, v => v.HasValue);
        var actualFirst = Array.FindIndex(actual, v => v.HasValue);
        Assert.True(expectedFirst >= 0, $"reference for {key} has no values");
        Assert.True(expectedFirst == actualFirst,
            $"{fixtureName} {key}: first value at bar {actualFirst}, reference at bar {expectedFirst}");

        // 2. Values within tolerance wherever the reference has a value.
        var relTol = RelTolFor(key);
        var mismatches = new List<string>();
        int compared = 0;
        for (int i = expectedFirst; i < expected.Length; i++)
        {
            if (!expected[i].HasValue) continue;
            compared++;
            if (!actual[i].HasValue)
            {
                mismatches.Add($"bar {i}: expected {expected[i]}, actual null");
                continue;
            }
            var e = expected[i]!.Value;
            var a = actual[i]!.Value;
            var tol = Math.Max(AbsFloor, Math.Abs(e) * relTol);
            if (double.IsNaN(a) || Math.Abs(a - e) > tol)
            {
                mismatches.Add($"bar {i} (t={fixture.Bars.Results[i].Timestamp}): expected {e:G10}, actual {a:G10}, diff {a - e:G4}");
            }
        }

        Assert.True(compared > 0, $"{fixtureName} {key}: nothing compared");
        Assert.True(mismatches.Count == 0,
            $"{fixtureName} {key}: {mismatches.Count}/{compared} bars outside tolerance (rel {relTol}). First 10:\n  " +
            string.Join("\n  ", mismatches.Take(10)));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Incremental_Matches_Full(string fixtureName, string key)
    {
        var fixture = GoldenFixture.Load(fixtureName);
        var timeframe = TimeframeFor(fixtureName);
        var full = Engine.EvaluateSeries(key, fixture.Bars, timeframe);

        // Seed with the first ~40% of bars (at least 250 for the 200-period indicators), then append one bar at a time.
        var all = fixture.CloneBars();
        var total = all.Results.Count;
        var seedCount = Math.Min(total - 1, Math.Max(250, total * 4 / 10));
        var pending = all.Results.GetRange(seedCount, total - seedCount);
        all.Results.RemoveRange(seedCount, total - seedCount);

        var session = Engine.Compile(key);
        var mismatches = new List<string>();

        for (int i = seedCount; i < total; i++)
        {
            all.Results.Add(pending[i - seedCount]);
            var raw = session.EvaluateIncrementalRaw(all, timeframe);
            var last = LastValue(raw, all.Results[^1].Timestamp);
            var expected = full[i];

            if (expected is null && last is null) continue;
            if (expected is null || last is null || Math.Abs(expected.Value - last.Value) > Math.Max(AbsFloor, Math.Abs(expected.Value) * 1e-9))
            {
                mismatches.Add($"bar {i}: full {expected?.ToString("G10") ?? "null"}, incremental {last?.ToString("G10") ?? "null"}");
                if (mismatches.Count >= 10) break;
            }
        }

        Assert.True(mismatches.Count == 0,
            $"{fixtureName} {key}: incremental evaluation diverges from full evaluation. First mismatches:\n  " +
            string.Join("\n  ", mismatches));
    }

    /// <summary>
    /// The backtester's multi-minute timeframes mutate the LAST bar in place every minute
    /// (<c>UpdateLatestCandle</c>) and evaluate incrementally after each mutation. Every value for
    /// that bar must track the mutation, not stay at what the candle looked like when it opened
    /// (plan 14 follow-up #1). Emulated here without the backtester: each pending bar is first
    /// appended as a stub "opening" bar (o=h=l=c=open, small volume), then grown through two
    /// intermediate shapes to its final shape, evaluating incrementally after each step; after the
    /// final step the incremental value must equal the full evaluation on the same bars.
    ///
    /// Every third bar the final-shape evaluation is SKIPPED (the next bar opens before the node
    /// is evaluated again) — that is what an AND/OR branch sees when the session short-circuits
    /// past it — so a point computed on a provisional shape must be recomputed even when its bar
    /// is no longer the last one. The whole final series is compared at the end to catch that.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void Incremental_Matches_Full_When_Last_Bar_Is_Mutated_In_Place(string fixtureName, string key)
    {
        var fixture = GoldenFixture.Load(fixtureName);
        var timeframe = TimeframeFor(fixtureName);
        var full = Engine.EvaluateSeries(key, fixture.Bars, timeframe);

        var all = fixture.CloneBars();
        var total = all.Results.Count;
        var seedCount = Math.Min(total - 1, Math.Max(250, total * 4 / 10));
        var pending = all.Results.GetRange(seedCount, total - seedCount);
        all.Results.RemoveRange(seedCount, total - seedCount);

        var session = Engine.Compile(key);
        var mismatches = new List<string>();

        for (int i = seedCount; i < total && mismatches.Count < 10; i++)
        {
            var final = pending[i - seedCount];

            // 1. the candle opens: only its first minute is known
            var forming = final.Clone();
            forming.High = forming.Low = forming.Close = forming.Open;
            forming.Vwap = forming.Open;
            forming.Volume = Math.Max(1, final.Volume / 10);
            all.Results.Add(forming);
            session.EvaluateIncrementalRaw(all, timeframe);

            // 2. it moves: two intermediate shapes, mutated IN PLACE like UpdateLatestCandle does
            foreach (var t in new[] { 0.35, 0.7 })
            {
                forming.Close = (float)(final.Open + (final.Close - final.Open) * t);
                forming.High = Math.Max(forming.High, forming.Close);
                forming.Low = Math.Min(forming.Low, forming.Close);
                forming.Vwap = (forming.High + forming.Low + forming.Close) / 3;
                forming.Volume = final.Volume * t;
                session.EvaluateIncrementalRaw(all, timeframe);
            }

            // 3. its final shape — must now agree with the full evaluation (unless this is a
            //    "skipped" bar: the node is not evaluated again until the next bar has opened)
            forming.Open = final.Open; forming.High = final.High; forming.Low = final.Low; forming.Close = final.Close;
            forming.Vwap = final.Vwap; forming.Volume = final.Volume; forming.TransactionCount = final.TransactionCount;
            if (i % 3 == 1) continue;

            var raw = session.EvaluateIncrementalRaw(all, timeframe);
            var last = LastValue(raw, all.Results[^1].Timestamp);
            var expected = full[i];

            if (expected is null && last is null) continue;
            if (expected is null || last is null || Math.Abs(expected.Value - last.Value) > Math.Max(AbsFloor, Math.Abs(expected.Value) * 1e-9))
            {
                mismatches.Add($"bar {i}: full {expected?.ToString("G10") ?? "null"}, incremental after mutation {last?.ToString("G10") ?? "null"}");
            }
        }

        Assert.True(mismatches.Count == 0,
            $"{fixtureName} {key}: incremental value on a mutated (forming) last bar is stale. First mismatches:\n  " +
            string.Join("\n  ", mismatches));

        // Whole-series check on the final state: points for skipped bars must have been recomputed.
        var finalIncremental = IndicatorExpressionEngine.AlignSeries(session.EvaluateIncrementalRaw(all, timeframe), all);
        for (int i = 0; i < total; i++)
        {
            var e = full[i];
            var a = finalIncremental[i];
            if (e is null && a is null) continue;
            if (e is null || a is null || Math.Abs(e.Value - a.Value) > Math.Max(AbsFloor, Math.Abs(e.Value) * 1e-9))
            {
                mismatches.Add($"bar {i}: full {e?.ToString("G10") ?? "null"}, final incremental series {a?.ToString("G10") ?? "null"}");
                if (mismatches.Count >= 10) break;
            }
        }
        Assert.True(mismatches.Count == 0,
            $"{fixtureName} {key}: final incremental series has stale points (skipped-evaluation bars). First mismatches:\n  " +
            string.Join("\n  ", mismatches));
    }

    private static double? LastValue(object raw, long lastBarTimestamp) => raw switch
    {
        // Timestamped series may legitimately skip bars (e.g. vwap() pre-market): the value "at"
        // the last bar exists only if the last point is for that bar.
        List<IIndicatorResult> series => series.Count == 0 || series[^1].Timestamp != lastBarTimestamp ? null : series[^1].GetFieldValue("value"),
        List<double> values => values.Count == 0 ? null : values[^1],
        double d => d,
        IIndicatorResult single => single.GetFieldValue("value"),
        _ => throw new InvalidOperationException($"Unexpected raw result {raw?.GetType().Name}")
    };

    private static double RelTolFor(string key)
    {
        if (key.StartsWith("sma(") || key.StartsWith("adv(") || key == "slope(close,5)") return SimpleRelTol;
        return RecursiveRelTol;
    }

    /// <summary>Fixture names are TICKER_tf_from_to; tf is like 1m / 1h / 1d.</summary>
    internal static Timeframe TimeframeFor(string fixtureName)
    {
        var tf = fixtureName.Split('_')[1];
        var multiplier = int.Parse(tf[..^1]);
        var span = tf[^1] switch
        {
            'm' => Timespan.minute,
            'h' => Timespan.hour,
            'd' => Timespan.day,
            _ => throw new ArgumentException($"Unknown timeframe unit in {fixtureName}")
        };
        return new Timeframe(multiplier, span);
    }
}
