using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market;
using Massive.Client.Models;

namespace MarketViewer.Filters.UnitTests.Golden;

/// <summary>
/// Replays a golden fixture through a compiled <see cref="Sessions.FilterSession"/> the way
/// <c>Backtest.Lambda.Services.ScannerService.GetResultsFromFilter</c> does: seed with history,
/// then feed one bar at a time and evaluate incrementally with the bar's time as the clock.
///
/// The replay window mirrors tools/golden/compute_outcomes.py exactly:
///  - 1-minute fixtures: for every ET trading date except the first, seed with all bars before
///    09:30 ET, then feed each bar with 09:30 &lt;= t &lt; 16:00 and evaluate.
///  - other fixtures: seed with the first <see cref="SeedBars"/> bars, then feed and evaluate the rest.
/// </summary>
internal static class GoldenReplay
{
    public const int SeedBars = 250;
    private static readonly TimeZoneInfo Eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    private static readonly IndicatorExpressionEngine Engine = new();

    public sealed record Result(List<long> TrueAt, int EvaluatedCount);

    public static Result Run(GoldenFixture fixture, string script)
    {
        var timeframe = GoldenIndicatorTests.TimeframeFor(fixture.Name);
        return timeframe is { Multiplier: 1, Timespan: Timespan.minute }
            ? RunIntradayByDate(fixture, script, timeframe)
            : RunSequential(fixture, script, timeframe);
    }

    private static Result RunSequential(GoldenFixture fixture, string script, Timeframe timeframe)
    {
        var all = fixture.CloneBars();
        var bars = all.Results;
        var seed = Math.Min(SeedBars, bars.Count);
        var pending = bars.GetRange(seed, bars.Count - seed);
        bars.RemoveRange(seed, bars.Count - seed);

        var session = Engine.Compile(script);
        var trueAt = new List<long>();
        foreach (var bar in pending)
        {
            bars.Add(bar);
            if (session.EvaluateIncremental(all, timeframe, evaluationTime: DateTimeOffset.FromUnixTimeMilliseconds(bar.Timestamp)))
            {
                trueAt.Add(bar.Timestamp);
            }
        }
        return new Result(trueAt, pending.Count);
    }

    private static Result RunIntradayByDate(GoldenFixture fixture, string script, Timeframe timeframe)
    {
        var source = fixture.Bars.Results;
        var dates = source.Select(b => EasternDate(b.Timestamp)).Distinct().OrderBy(d => d).ToList();

        var trueAt = new List<long>();
        int evaluated = 0;

        foreach (var date in dates.Skip(1))
        {
            var open = new DateTimeOffset(date, new TimeOnly(9, 30), Eastern.GetUtcOffset(date.ToDateTime(new TimeOnly(9, 30)))).ToUnixTimeMilliseconds();
            var close = new DateTimeOffset(date, new TimeOnly(16, 0), Eastern.GetUtcOffset(date.ToDateTime(new TimeOnly(16, 0)))).ToUnixTimeMilliseconds();

            var history = new StocksResponse
            {
                Ticker = fixture.Bars.Ticker,
                Status = fixture.Bars.Status,
                Results = source.Where(b => b.Timestamp < open).Select(b => b.Clone()).ToList()
            };
            var sessionBars = source.Where(b => b.Timestamp >= open && b.Timestamp < close).ToList();

            var session = Engine.Compile(script);
            foreach (var bar in sessionBars)
            {
                history.Results.Add(bar.Clone());
                evaluated++;
                if (session.EvaluateIncremental(history, timeframe, evaluationTime: DateTimeOffset.FromUnixTimeMilliseconds(bar.Timestamp)))
                {
                    trueAt.Add(bar.Timestamp);
                }
            }
        }

        return new Result(trueAt, evaluated);
    }

    private static DateOnly EasternDate(long timestamp)
    {
        var eastern = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(timestamp), Eastern);
        return DateOnly.FromDateTime(eastern.DateTime);
    }
}
