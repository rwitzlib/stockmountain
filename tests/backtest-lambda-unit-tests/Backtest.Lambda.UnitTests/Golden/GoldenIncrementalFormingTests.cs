using Backtest.Lambda.Utilities;
using FluentAssertions;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Filters;
using MarketViewer.Filters.Interfaces;
using Timespan = MarketViewer.Contracts.Enums.Timespan;

namespace Backtest.Lambda.UnitTests.Golden;

/// <summary>
/// Plan 14 follow-up #1. On a multi-minute timeframe the scanner mutates the last candle in place
/// every minute (<see cref="StocksResponseExtensions.UpdateLatestCandle"/>) and evaluates the
/// compiled filter incrementally. Every series and every boolean the filter produces must then
/// reflect the candle as it is NOW, not as it was when it opened: after each minute the incremental
/// session's answer must equal a fresh full evaluation on the very same bars.
/// </summary>
public class GoldenIncrementalFormingTests
{
    private static readonly IndicatorExpressionEngine Engine = new();

    // Series scripts: last incremental value == last full value.
    private static readonly string[] SeriesScripts =
    [
        "close", "high", "low", "volume",
        "sma(20)", "ema(20)", "adv(30)",
        "rsi(14,70,30,wilders)", "rsi(14,70,30,sma)",
        "macd(12,26,9,ema).value", "macd(12,26,9,ema).signal", "macd(12,26,9,ema).histogram", "macd(12,26,9,sma).histogram",
        "slope(close,5)", "slope(sma(5),5)", "slope(ema(5),3)",
        "vwap()", "vwap(day)",
    ];

    // Whole filters: incremental bool == full bool.
    private static readonly string[] FilterScripts =
    [
        "close > sma(20)",
        "close > vwap() AND volume > adv(30)",
        "rsi(14,70,30,wilders) < 40 OR macd(12,26,9,ema).histogram > 0",
        "crosses_over(close, sma(20))",
        "crosses_under(ema(9), ema(20))",
        "slope(sma(5),5) > 0 AND close > open",
    ];

    public static IEnumerable<object[]> FixtureTimeframes()
    {
        foreach (var name in GoldenData.MinuteFixtures())
        {
            yield return [name, 5, Timespan.minute];
            yield return [name, 15, Timespan.minute];
            yield return [name, 1, Timespan.hour];
        }
    }

    [Theory]
    [MemberData(nameof(FixtureTimeframes))]
    public void Series_On_Forming_Candle_Match_Full_Evaluation_After_Every_Minute(string fixtureName, int multiplier, Timespan span)
    {
        var timeframe = new Timeframe(multiplier, span);
        var mismatches = new List<string>();

        foreach (var script in SeriesScripts)
        {
            var (response, sessionMinutes, open) = SeedLastDay(fixtureName, timeframe);
            var session = Engine.Compile(script);
            var scriptTf = $"{script} [{multiplier}{TfUnit(span)}]";
            int compared = 0;

            foreach (var minute in sessionMinutes)
            {
                response.UpdateLatestCandle(timeframe, minute.Clone());
                var evalTime = DateTimeOffset.FromUnixTimeMilliseconds(minute.Timestamp);

                var incremental = LastValue(session.EvaluateIncrementalRaw(response, timeframe, evaluationTime: evalTime), response.Results[^1].Timestamp);
                var full = Engine.EvaluateSeries(script, response, timeframe, evalTime)[^1];

                if (full is null && incremental is null) continue;
                compared++;
                if (full is null || incremental is null || Math.Abs(full.Value - incremental.Value) > Math.Max(1e-6, Math.Abs(full.Value) * 1e-9))
                {
                    mismatches.Add($"{scriptTf} at {Eastern(minute.Timestamp):HH:mm}: full {full?.ToString("G10") ?? "null"}, incremental {incremental?.ToString("G10") ?? "null"}");
                    if (mismatches.Count >= 15) break;
                }
            }

            compared.Should().BeGreaterThan(0, $"{fixtureName} {scriptTf} should have produced values during the session (open {Eastern(open):yyyy-MM-dd})");
            if (mismatches.Count >= 15) break;
        }

        mismatches.Should().BeEmpty($"{fixtureName}: incremental values on the forming candle went stale:\n  " + string.Join("\n  ", mismatches));
    }

    [Theory]
    [MemberData(nameof(FixtureTimeframes))]
    public void Filters_On_Forming_Candle_Match_Full_Evaluation_After_Every_Minute(string fixtureName, int multiplier, Timespan span)
    {
        var timeframe = new Timeframe(multiplier, span);
        var mismatches = new List<string>();

        foreach (var script in FilterScripts)
        {
            var (response, sessionMinutes, _) = SeedLastDay(fixtureName, timeframe);
            var session = Engine.Compile(script);
            var scriptTf = $"{script} [{multiplier}{TfUnit(span)}]";

            foreach (var minute in sessionMinutes)
            {
                response.UpdateLatestCandle(timeframe, minute.Clone());
                var evalTime = DateTimeOffset.FromUnixTimeMilliseconds(minute.Timestamp);

                var incremental = session.EvaluateIncremental(response, timeframe, evaluationTime: evalTime);
                var full = Engine.EvaluateScript(script, response, timeframe, evaluationTime: evalTime);

                if (incremental != full)
                {
                    mismatches.Add($"{scriptTf} at {Eastern(minute.Timestamp):HH:mm}: full {full}, incremental {incremental}");
                    if (mismatches.Count >= 15) break;
                }
            }
            if (mismatches.Count >= 15) break;
        }

        mismatches.Should().BeEmpty($"{fixtureName}: incremental filter results on the forming candle went stale:\n  " + string.Join("\n  ", mismatches));
    }

    // ---- helpers

    /// <summary>
    /// History = clock-aligned candles from every minute before the LAST date's 09:30 ET (several days,
    /// so 20/26/30-period indicators are warm); session = that date's 09:30–16:00 minutes.
    /// </summary>
    private static (StocksResponse Response, List<Massive.Client.Models.Bar> SessionMinutes, long Open) SeedLastDay(string fixtureName, Timeframe timeframe)
    {
        var fixture = GoldenData.Bars(fixtureName);
        var date = GoldenData.Dates(fixture)[^1];
        var open = GoldenData.EasternTime(date, 9, 30).ToUnixTimeMilliseconds();
        var close = GoldenData.EasternTime(date, 16, 0).ToUnixTimeMilliseconds();

        var history = fixture.Results.Where(b => b.Timestamp < open).ToList();
        var sessionMinutes = fixture.Results.Where(b => b.Timestamp >= open && b.Timestamp < close).ToList();
        history.Should().NotBeEmpty();
        sessionMinutes.Should().NotBeEmpty();

        var response = new StocksResponse { Ticker = fixture.Ticker, Results = GoldenCandleFormingTests.Aggregate(history, timeframe) };
        return (response, sessionMinutes, open);
    }

    private static double? LastValue(object raw, long lastBarTimestamp) => raw switch
    {
        List<IIndicatorResult> series => series.Count == 0 || series[^1].Timestamp != lastBarTimestamp ? null : series[^1].GetFieldValue("value"),
        List<double> values => values.Count == 0 ? null : values[^1],
        double d => d,
        _ => throw new InvalidOperationException($"Unexpected raw result {raw?.GetType().Name}")
    };

    private static string TfUnit(Timespan span) => span switch { Timespan.minute => "m", Timespan.hour => "h", _ => "d" };

    private static DateTimeOffset Eastern(long timestamp) =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(timestamp), GoldenData.Eastern);
}
