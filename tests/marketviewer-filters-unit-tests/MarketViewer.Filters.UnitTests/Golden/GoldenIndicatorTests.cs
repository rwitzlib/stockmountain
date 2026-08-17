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
            var last = LastValue(raw);
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

    private static double? LastValue(object raw) => raw switch
    {
        List<IIndicatorResult> series => series.Count == 0 ? null : series[^1].GetFieldValue("value"),
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
