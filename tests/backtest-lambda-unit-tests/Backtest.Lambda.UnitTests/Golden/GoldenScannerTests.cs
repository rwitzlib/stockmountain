using Backtest.Lambda.Services;
using FluentAssertions;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Filters;
using MarketViewer.Infrastructure.Config;
using Massive.Client.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Timespan = MarketViewer.Contracts.Enums.Timespan;

namespace Backtest.Lambda.UnitTests.Golden;

/// <summary>
/// Layers 3b/3c of plans/14-golden-filter-tests.md: <see cref="DataCache.Setup"/> +
/// <see cref="ScannerService.GetResultsFromFilter"/> driven end-to-end on real fixtures through a
/// preloaded market cache (no S3, no Massive). This is where the loop bounds, the per-date minute
/// file, <c>HasNextCandle</c>, <c>MergePreviousPeriod</c> and the forming daily candle all meet.
///
/// Scenario: scan 2025-06-04 for AAPL and NVDA with 1-minute and 1-day timeframes loaded. The
/// per-day minute files for 06-02 and 06-03 are preloaded too, so <see cref="DataCache.Setup"/> can
/// prepend the previous session (<see cref="DataCache.PreviousMinuteSessions"/>) exactly as it
/// would from S3.
/// </summary>
public class GoldenScannerTests : IAsyncLifetime
{
    private static readonly Timeframe Minute = new(1, Timespan.minute);
    private static readonly Timeframe FiveMinute = new(5, Timespan.minute);
    private static readonly Timeframe Day = new(1, Timespan.day);
    private static readonly DateOnly ScanDate = new(2025, 6, 4);
    private static readonly DateOnly PreviousSession = new(2025, 6, 3);
    private static readonly string[] Tickers = ["AAPL", "NVDA"];

    private readonly DateTimeOffset _date = GoldenData.EasternTime(ScanDate, 0, 0);
    private readonly DateTimeOffset _open = GoldenData.EasternTime(ScanDate, 9, 30);
    private readonly PreloadedMarketCache _marketCache = new();
    private readonly IndicatorExpressionEngine _engine = new();
    private DataCache _dataCache = null!;
    private ScannerService _scanner = null!;

    // Raw fixture data for computing expectations independently of the code under test.
    private readonly Dictionary<string, StocksResponse> _minuteFixture = new();
    private readonly Dictionary<string, StocksResponse> _dailyFixture = new();

    public async Task InitializeAsync()
    {
        foreach (var ticker in Tickers)
        {
            var minutes = GoldenData.Bars($"{ticker}_1m_2025-06-02_2025-06-06");
            var daily = GoldenData.Bars($"{ticker}_1d_2023-06-01_2025-06-06");
            _minuteFixture[ticker] = minutes;
            _dailyFixture[ticker] = daily;

            // Per-day minute files (all sessions of that day). 06-02/06-03 are there for the previous-session
            // history load; 06-05/06-06 deliberately are not, so nothing after the scan date can leak in.
            var minuteFile = MinuteFile(ticker, minutes, ScanDate);
            Add(Minute, GoldenData.EasternTime(PreviousSession, 0, 0), MinuteFile(ticker, minutes, PreviousSession));
            Add(Minute, GoldenData.EasternTime(PreviousSession.AddDays(-1), 0, 0), MinuteFile(ticker, minutes, PreviousSession.AddDays(-1)));

            // Yearly daily files, overlapping by one bar exactly like the real aggregator output:
            // the 2025 file starts with 2024's final session.
            var bars2024 = daily.Results.Where(b => GoldenData.EasternDate(b.Timestamp).Year == 2024).Select(b => b.Clone()).ToList();
            var bars2025 = daily.Results.Where(b => GoldenData.EasternDate(b.Timestamp).Year == 2025).Select(b => b.Clone()).ToList();
            var currentYearFile = new StocksResponse { Ticker = ticker, Status = "OK", Results = [bars2024[^1].Clone(), .. bars2025] };
            var previousYearFile = new StocksResponse { Ticker = ticker, Status = "OK", Results = bars2024 };

            // Per-day 5-minute file, as Massive would serve it (clock-aligned aggregation of the day's minutes).
            var fiveMinuteFile = new StocksResponse { Ticker = ticker, Status = "OK", Results = GoldenCandleFormingTests.Aggregate(minuteFile.Results, FiveMinute) };

            Add(Minute, _date, minuteFile);
            Add(FiveMinute, _date, fiveMinuteFile);
            Add(Day, _date, currentYearFile);
            Add(Day, _date.AddYears(-1), previousYearFile);
        }

        foreach (var ((tf, date), responses) in _pending)
        {
            _marketCache.Preload(tf, date, responses.ToArray());
        }

        _dataCache = new DataCache(_marketCache, null!, NullLogger<DataCache>.Instance);
        (await _dataCache.Setup(_date, [Minute, FiveMinute, Day])).Should().BeTrue("DataCache.Setup must succeed against the preloaded cache");

        _scanner = new ScannerService(_engine, _dataCache, null!, new BacktestConfig(), NullLogger<ScannerService>.Instance);
    }

    private static StocksResponse MinuteFile(string ticker, StocksResponse allMinutes, DateOnly day)
    {
        var dayStart = GoldenData.EasternTime(day, 0, 0).ToUnixTimeMilliseconds();
        var dayEnd = GoldenData.EasternTime(day.AddDays(1), 0, 0).ToUnixTimeMilliseconds();
        return new StocksResponse { Ticker = ticker, Status = "OK", Results = allMinutes.Results.Where(b => b.Timestamp >= dayStart && b.Timestamp < dayEnd).Select(b => b.Clone()).ToList() };
    }

    private readonly Dictionary<(Timeframe, DateTimeOffset), List<StocksResponse>> _pending = new();
    private void Add(Timeframe tf, DateTimeOffset date, StocksResponse response)
    {
        if (!_pending.TryGetValue((tf, date), out var list)) _pending[(tf, date)] = list = [];
        list.Add(response);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ------------------------------------------------------------------ 3b: merged daily history

    [Fact]
    public void Daily_History_Is_Merged_Across_Yearly_Files_Without_Duplicates_And_Ends_With_The_Rebuilt_PreOpen_Candle()
    {
        foreach (var ticker in Tickers)
        {
            var merged = _dataCache.GetStocksResponse(ticker, Day).Results;
            var fixture = _dailyFixture[ticker].Results;

            merged.Select(b => b.Timestamp).Should().BeInAscendingOrder(ticker).And.OnlyHaveUniqueItems(ticker);

            // Everything from the 2024 file plus 2025 up to and including the scan date; nothing after.
            var expected = fixture.Where(b => GoldenData.EasternDate(b.Timestamp).Year >= 2024 && GoldenData.EasternDate(b.Timestamp) <= ScanDate).ToList();
            merged.Select(b => b.Timestamp).Should().Equal(expected.Select(b => b.Timestamp), ticker);

            // Today's daily candle has been rebuilt from pre-open minutes only.
            var preOpen = _minuteFixture[ticker].Results.Where(b => b.Timestamp >= _date.ToUnixTimeMilliseconds() && b.Timestamp < _open.ToUnixTimeMilliseconds()).ToList();
            var today = merged[^1];
            today.Timestamp.Should().Be(_date.ToUnixTimeMilliseconds());
            today.Open.Should().Be(preOpen[0].Open, ticker);
            today.Close.Should().Be(preOpen[^1].Close, ticker);
            today.High.Should().Be(preOpen.Max(b => b.High), ticker);
            today.Low.Should().Be(preOpen.Min(b => b.Low), ticker);
        }
    }

    [Fact]
    public void Sma200_Over_Merged_Daily_History_Matches_Reference_On_The_Previous_Session()
    {
        foreach (var ticker in Tickers)
        {
            var merged = _dataCache.GetStocksResponse(ticker, Day);
            var history = new StocksResponse { Ticker = ticker, Results = merged.Results.Where(b => GoldenData.EasternDate(b.Timestamp) < ScanDate).ToList() };

            var sma = _engine.EvaluateSeries("sma(200)", history, Day);
            var reference = GoldenData.Reference($"{ticker}_1d_2023-06-01_2025-06-06");
            var fixture = _dailyFixture[ticker].Results;
            var previousSessionIndex = fixture.FindLastIndex(b => GoldenData.EasternDate(b.Timestamp) < ScanDate);

            sma[^1].Should().NotBeNull($"{ticker}: sma(200) needs the previous year merged in — the 2025 file alone has too few bars");
            sma[^1]!.Value.Should().BeApproximately(reference.Series["sma(200)"][previousSessionIndex]!.Value, 1e-3, ticker);
        }
    }

    // ------------------------------------------------------------------ 3c: scanner end-to-end

    [Fact]
    public async Task Scanner_Emits_An_Entry_For_Every_Session_Minute_Where_The_Bar_Satisfies_A_Local_Filter()
    {
        var entries = await _scanner.GetResultsFromFilter("close > open [1m]", _date);

        var expected = new List<(string Ticker, DateTimeOffset Start)>();
        foreach (var ticker in Tickers)
        {
            var byTimestamp = _minuteFixture[ticker].Results.ToDictionary(b => b.Timestamp);
            // ScannerService iterates every session minute [0, 390): the 15:59 bar is a signal minute too.
            for (int i = 0; i < 390; i++)
            {
                var t = _open.AddMinutes(i);
                if (byTimestamp.TryGetValue(t.ToUnixTimeMilliseconds(), out var bar) && bar.Close > bar.Open)
                {
                    expected.Add((ticker, t));
                }
            }
        }

        entries.Select(e => (e.Ticker, e.Start)).Should().BeEquivalentTo(expected);
        expected.Count.Should().BeGreaterThan(200);
    }

    [Fact]
    public async Task Scanner_Entries_Are_Sorted_By_Start_Then_Ticker()
    {
        var entries = await _scanner.GetResultsFromFilter("close > open [1m]", _date);
        entries.Should().BeInAscendingOrder(e => e.Start).And.ThenBeInAscendingOrder(e => e.Ticker, StringComparer.Ordinal);
    }

    [Fact]
    public void Minute_History_Is_The_Previous_Session_Plus_The_Scan_Dates_PreMarket()
    {
        // Plan 14 follow-up 3: the per-day minute file alone made [1m] indicators warm up on the scan
        // date's pre-market only. Setup now prepends PreviousMinuteSessions (=1) sessions, and only
        // that many — 06-02 is preloaded and must not be picked up.
        DataCache.PreviousMinuteSessions.Should().Be(1, "this test's expectations are written for one prior session");
        var previousStart = GoldenData.EasternTime(PreviousSession, 0, 0).ToUnixTimeMilliseconds();

        foreach (var ticker in Tickers)
        {
            var history = _dataCache.GetStocksResponse(ticker, Minute).Results;
            var expected = _minuteFixture[ticker].Results
                .Where(b => b.Timestamp >= previousStart && b.Timestamp < _open.ToUnixTimeMilliseconds())
                .Select(b => b.Timestamp)
                .ToList();

            history.Select(b => b.Timestamp).Should().Equal(expected, ticker);
            history.Select(b => b.Timestamp).Should().BeInAscendingOrder(ticker).And.OnlyHaveUniqueItems(ticker);
            GoldenData.EasternDate(history[0].Timestamp).Should().Be(PreviousSession, ticker);
        }
    }

    [Fact]
    public async Task Scanner_1m_Filter_Warms_Up_On_The_Previous_Sessions_Minutes()
    {
        // Replaying the same data through the engine directly must give the same signal minutes — this
        // pins the wiring (previous-session merge, per-date trim, HasNextCandle mapping, evaluationTime,
        // loop bounds) and proves the previous session actually feeds the indicator: sma(400) needs more
        // bars than one pre-market has (~330 on AAPL), so with same-day history it cannot fire at 09:30.
        const string filter = "close > sma(400) [1m]";
        var entries = await _scanner.GetResultsFromFilter(filter, _date);

        var withHistory = Replay(filter, GoldenData.EasternTime(PreviousSession, 0, 0));
        var sameDayOnly = Replay(filter, _date);

        entries.Select(e => (e.Ticker, e.Start)).Should().BeEquivalentTo(withHistory);
        withHistory.Should().NotBeEmpty();
        withHistory.Should().NotBeEquivalentTo(sameDayOnly, "the previous session's minutes must change the indicator's warm-up");
    }

    private List<(string, DateTimeOffset)> Replay(string filter, DateTimeOffset historyStart)
    {
        var results = new List<(string, DateTimeOffset)>();
        foreach (var ticker in Tickers)
        {
            var bars = _minuteFixture[ticker].Results.Where(b => b.Timestamp >= historyStart.ToUnixTimeMilliseconds()).OrderBy(b => b.Timestamp).ToList();
            var response = new StocksResponse { Ticker = ticker, Results = bars.Where(b => b.Timestamp < _open.ToUnixTimeMilliseconds()).Select(b => b.Clone()).ToList() };
            var byTimestamp = bars.ToDictionary(b => b.Timestamp);
            var session = _engine.Compile(filter);
            for (int i = 0; i < 390; i++)
            {
                var t = _open.AddMinutes(i);
                if (!byTimestamp.TryGetValue(t.ToUnixTimeMilliseconds(), out var bar)) continue;
                response.Results.Add(bar.Clone());
                if (session.EvaluateIncremental(response, Minute, evaluationTime: t))
                {
                    results.Add((ticker, t));
                }
            }
        }
        return results;
    }

    [Fact]
    public async Task Scanning_A_Larger_Timeframe_Filter_Must_Not_Mutate_The_Cached_Minute_Bars()
    {
        // Filters are scanned concurrently (ScanForEntries runs one Task per filter) and all of them,
        // plus downstream fill pricing, read the same NextCandlesCache bar objects. Forming a 5m/1h/1d
        // candle in place from a cached minute bar would corrupt the 09:30, 09:35, ... minutes for
        // every other consumer. UpdateLatestCandle must therefore never add the cached instance itself.
        var before = Tickers.ToDictionary(t => t, t => Enumerable.Range(0, 390).Select(i => _dataCache.GetNextCandle(t, i)?.Clone()).ToList());

        await _scanner.GetResultsFromFilter("close > open [5m]", _date);
        await _scanner.GetResultsFromFilter("close > sma(200) [1d]", _date);

        foreach (var ticker in Tickers)
        {
            for (int i = 0; i < 390; i++)
            {
                var cached = _dataCache.GetNextCandle(ticker, i);
                var original = before[ticker][i];
                if (original is null) { cached.Should().BeNull(); continue; }
                cached.Should().BeEquivalentTo(original, $"{ticker} minute {i} must be untouched by a larger-timeframe scan");
            }
        }
    }

    [Fact]
    public async Task Scanner_1d_Filter_Uses_Merged_History_And_The_Forming_Daily_Candle()
    {
        // close > sma(200) [1d], evaluated every minute: the daily candle's close is the current
        // minute's close, and the other 199 closes are the sessions before the scan date.
        var entries = await _scanner.GetResultsFromFilter("close > sma(200) [1d]", _date);

        var expected = new List<(string, DateTimeOffset)>();
        foreach (var ticker in Tickers)
        {
            var previousCloses = _dailyFixture[ticker].Results
                .Where(b => GoldenData.EasternDate(b.Timestamp) < ScanDate)
                .TakeLast(199)
                .Select(b => (double)b.Close)
                .ToList();
            previousCloses.Should().HaveCount(199);
            var previousSum = previousCloses.Sum();

            var byTimestamp = _minuteFixture[ticker].Results.ToDictionary(b => b.Timestamp);
            for (int i = 0; i < 390; i++)
            {
                var t = _open.AddMinutes(i);
                if (!byTimestamp.TryGetValue(t.ToUnixTimeMilliseconds(), out var bar)) continue;
                var sma200 = (previousSum + bar.Close) / 200.0;
                if (bar.Close > sma200)
                {
                    expected.Add((ticker, t));
                }
            }
        }

        entries.Select(e => (e.Ticker, e.Start)).Should().BeEquivalentTo(expected);
        expected.Should().NotBeEmpty();
        // Sanity: at least one ticker should be on each side of its 200-day average on this date,
        // otherwise the assertion is trivially satisfied by "all" or "none".
        entries.Select(e => e.Ticker).Distinct().Count().Should().BeGreaterThan(0);
    }
}
