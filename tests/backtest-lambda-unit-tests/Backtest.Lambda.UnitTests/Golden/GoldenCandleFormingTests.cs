using Backtest.Lambda.Services;
using Backtest.Lambda.Utilities;
using FluentAssertions;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market;
using Massive.Client.Models;
using Timespan = MarketViewer.Contracts.Enums.Timespan;

namespace Backtest.Lambda.UnitTests.Golden;

/// <summary>
/// Layer 3a of plans/14-golden-filter-tests.md. The backtester never rebuilds larger-timeframe
/// candles from minutes wholesale — it takes them from the market cache, rebuilds only the candle
/// that spans market open (<see cref="DataCache.RebuildOverlappingCandle"/>), and then forms the
/// current candle minute-by-minute (<see cref="StocksResponseExtensions.UpdateLatestCandle"/>).
/// These tests drive both against real minute fixtures (incl. the DST-change and half-day ones)
/// and compare with a plain clock-aligned OHLCV aggregation.
/// </summary>
public class GoldenCandleFormingTests
{
    // VWAP is carried as float32 on the bar and merged minute-by-minute as a weighted recurrence
    // (BarVwap.Merge), re-rounded to float32 at every step, versus one double Σ(vw·v)/Σv here. Over a
    // full day of minutes into one daily candle that drifts a few e-6 relative (observed 2.05e-6 on
    // NVDA 1d); 1e-5 is still ~100× tighter than the typical-price approximation it replaced.
    private static float VwapTolerance(float expected) => Math.Max(1e-4f, Math.Abs(expected) * 1e-5f);

    public static IEnumerable<object[]> FixtureTimeframes()
    {
        foreach (var name in GoldenData.MinuteFixtures())
        {
            yield return [name, 5, Timespan.minute];
            yield return [name, 15, Timespan.minute];
            yield return [name, 1, Timespan.hour];
            yield return [name, 1, Timespan.day];
        }
    }

    [Theory]
    [MemberData(nameof(FixtureTimeframes))]
    public void UpdateLatestCandle_Forms_Candles_That_Match_Clock_Aligned_Aggregation(string fixtureName, int multiplier, Timespan span)
    {
        var timeframe = new Timeframe(multiplier, span);
        var fixture = GoldenData.Bars(fixtureName);
        var minutes = fixture.Results;

        foreach (var date in GoldenData.Dates(fixture))
        {
            var open = GoldenData.EasternTime(date, 9, 30).ToUnixTimeMilliseconds();
            var close = GoldenData.EasternTime(date, 16, 0).ToUnixTimeMilliseconds();
            var nextMidnight = GoldenData.EasternTime(date.AddDays(1), 0, 0).ToUnixTimeMilliseconds();

            // What the scan starts with: pre-open candles for this date only, in the market-cache
            // shape (aligned to the clock; the open-spanning one already trimmed to pre-open minutes).
            var dayMinutes = minutes.Where(b => b.Timestamp >= GoldenData.EasternTime(date, 0, 0).ToUnixTimeMilliseconds() && b.Timestamp < nextMidnight).ToList();
            var preOpen = dayMinutes.Where(b => b.Timestamp < open).ToList();
            if (preOpen.Count == 0)
            {
                continue; // no pre-open state to seed from on this date; the forming logic needs a last candle
            }

            var response = new StocksResponse { Ticker = fixture.Ticker, Results = Aggregate(preOpen, timeframe) };
            var seen = new List<Bar>(preOpen);

            // Feed the whole rest of the day (regular session AND after-hours) — the scanner only
            // feeds the session, but the forming rule must hold for any minute of the day.
            foreach (var minute in dayMinutes.Where(b => b.Timestamp >= open))
            {
                response.UpdateLatestCandle(timeframe, minute.Clone());
                seen.Add(minute);

                var expected = Aggregate(seen, timeframe);
                var context = $"{fixtureName} {multiplier}{span} {date} after minute {Eastern(minute.Timestamp):HH:mm}";

                response.Results.Count.Should().Be(expected.Count, context);
                AssertSameCandle(response.Results[^1], expected[^1], context);
            }

            // Every candle (not just the last) must match after the day is fully formed.
            var finalExpected = Aggregate(seen, timeframe);
            for (int i = 0; i < finalExpected.Count; i++)
            {
                AssertSameCandle(response.Results[i], finalExpected[i], $"{fixtureName} {multiplier}{span} {date} candle {i}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(FixtureTimeframes))]
    public void RebuildOverlappingCandle_Trims_The_Open_Spanning_Candle_To_PreOpen_Minutes(string fixtureName, int multiplier, Timespan span)
    {
        var timeframe = new Timeframe(multiplier, span);
        var fixture = GoldenData.Bars(fixtureName);
        var minutes = fixture.Results;

        foreach (var date in GoldenData.Dates(fixture))
        {
            var marketOpen = GoldenData.EasternTime(date, 9, 30);
            var openMs = marketOpen.ToUnixTimeMilliseconds();
            var dayStart = GoldenData.EasternTime(date, 0, 0).ToUnixTimeMilliseconds();
            var dayEnd = GoldenData.EasternTime(date.AddDays(1), 0, 0).ToUnixTimeMilliseconds();
            var dayMinutes = minutes.Where(b => b.Timestamp >= dayStart && b.Timestamp < dayEnd).ToList();

            // As stored by the market cache: full-day candles, including post-open data.
            var stored = Aggregate(dayMinutes, timeframe);
            var response = new StocksResponse { Ticker = fixture.Ticker, Results = stored };

            // The scan drops candles at/after open, then rebuilds the one that spans it.
            response.Results.RemoveAll(c => c.Timestamp >= openMs);
            if (response.Results.Count == 0) continue;

            var overlaps = DataCache.CheckIfCurrentCandleOverlapsMarketOpen(response, marketOpen, timeframe, out var lastCandle);
            var lastStart = lastCandle.Timestamp;
            var lastEnd = BucketEnd(lastStart, timeframe);
            overlaps.Should().Be(lastStart < openMs && lastEnd > openMs, $"{fixtureName} {multiplier}{span} {date}");

            if (!overlaps) continue;

            DataCache.RebuildOverlappingCandle(lastCandle, dayMinutes, marketOpen);

            var preOpenInBucket = dayMinutes.Where(b => b.Timestamp >= lastStart && b.Timestamp < openMs).ToList();
            preOpenInBucket.Should().NotBeEmpty($"{fixtureName} {date}: the bucket spanning open must have pre-open minutes in a liquid name");
            AssertSameCandle(lastCandle, GoldenData.Aggregate(lastStart, preOpenInBucket), $"{fixtureName} {multiplier}{span} {date} rebuilt candle");
        }
    }

    [Fact]
    public void RebuildOverlappingCandle_Zeroes_Candle_When_No_PreOpen_Minutes()
    {
        var fixture = GoldenData.Bars("AAPL_1m_2025-06-02_2025-06-06");
        var date = GoldenData.Dates(fixture)[1];
        var open = GoldenData.EasternTime(date, 9, 30);
        var sessionOnly = fixture.Results.Where(b => b.Timestamp >= open.ToUnixTimeMilliseconds()).ToList();

        var candle = new Bar { Timestamp = GoldenData.EasternTime(date, 0, 0).ToUnixTimeMilliseconds(), Open = 1, High = 2, Low = 0.5f, Close = 1.5f, Volume = 99, TransactionCount = 9 };
        DataCache.RebuildOverlappingCandle(candle, sessionOnly, open);

        candle.Open.Should().Be(0);
        candle.High.Should().Be(0);
        candle.Low.Should().Be(0);
        candle.Close.Should().Be(0);
        candle.Volume.Should().Be(0);
        candle.TransactionCount.Should().Be(0);
    }

    // ---- helpers

    /// <summary>Clock-aligned OHLCV aggregation in Eastern time (5m/15m on the hour's multiples, 1h at :00, 1d at midnight ET).</summary>
    internal static List<Bar> Aggregate(IEnumerable<Bar> minutes, Timeframe timeframe)
    {
        return minutes
            .GroupBy(b => BucketStart(b.Timestamp, timeframe))
            .OrderBy(g => g.Key)
            .Select(g => GoldenData.Aggregate(g.Key, g.OrderBy(b => b.Timestamp).ToList()))
            .ToList();
    }

    internal static long BucketStart(long timestamp, Timeframe timeframe)
    {
        var et = Eastern(timestamp);
        var floored = timeframe.Timespan switch
        {
            Timespan.minute => new DateTime(et.Year, et.Month, et.Day, et.Hour, et.Minute - et.Minute % timeframe.Multiplier, 0),
            Timespan.hour => new DateTime(et.Year, et.Month, et.Day, et.Hour - et.Hour % timeframe.Multiplier, 0, 0),
            Timespan.day => new DateTime(et.Year, et.Month, et.Day, 0, 0, 0),
            _ => throw new NotSupportedException()
        };
        return new DateTimeOffset(floored, GoldenData.Eastern.GetUtcOffset(floored)).ToUnixTimeMilliseconds();
    }

    private static long BucketEnd(long start, Timeframe timeframe)
    {
        var s = DateTimeOffset.FromUnixTimeMilliseconds(start);
        return (timeframe.Timespan switch
        {
            Timespan.minute => s.AddMinutes(timeframe.Multiplier),
            Timespan.hour => s.AddHours(timeframe.Multiplier),
            Timespan.day => s.AddDays(timeframe.Multiplier),
            _ => throw new NotSupportedException()
        }).ToUnixTimeMilliseconds();
    }

    private static DateTimeOffset Eastern(long timestamp) =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(timestamp), GoldenData.Eastern);

    private static void AssertSameCandle(Bar actual, Bar expected, string context)
    {
        actual.Timestamp.Should().Be(expected.Timestamp, context);
        actual.Open.Should().Be(expected.Open, context);
        actual.High.Should().Be(expected.High, context);
        actual.Low.Should().Be(expected.Low, context);
        actual.Close.Should().Be(expected.Close, context);
        // Bar.Volume is double: integer share counts sum exactly, so no tolerance (plan 14 follow-up #9).
        actual.Volume.Should().Be(expected.Volume, context);
        actual.Vwap.Should().BeApproximately(expected.Vwap, VwapTolerance(expected.Vwap), context);
        actual.TransactionCount.Should().Be(expected.TransactionCount, context);
    }
}
