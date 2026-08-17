using Amazon.S3;
using Backtest.Lambda.Services;
using Backtest.Lambda.UnitTests.Golden;
using FluentAssertions;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market;
using Massive.Client.Models;
using Massive.Client.Responses;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using Timespan = MarketViewer.Contracts.Enums.Timespan;

namespace Backtest.Lambda.UnitTests.Services;

/// <summary>
/// Plan 14 follow-up 3: the backtester prepends the previous session's minute file to the scan
/// date's so 1-minute indicators are warm at 09:30. "Previous session" is found by walking back
/// over weekends and over dates whose per-day file does not exist (holidays).
/// </summary>
public class DataCachePreviousMinuteSessionsUnitTests
{
    private static readonly Timeframe Minute = new(1, Timespan.minute);
    private static readonly TimeSpan Eastern = TimeSpan.FromHours(-4);

    private static DateTimeOffset Day(int year, int month, int day) => new(year, month, day, 0, 0, 0, Eastern);

    [Fact]
    public async Task Previous_Session_Is_The_Prior_Weekday_When_Its_File_Exists()
    {
        var cache = new PreloadedMarketCache();
        var scanDate = Day(2025, 6, 4); // Wednesday
        Preload(cache, scanDate, Day(2025, 6, 3), Day(2025, 6, 2));
        var dataCache = new DataCache(cache, null!, NullLogger<DataCache>.Instance);

        var sessions = await dataCache.LoadPreviousMinuteSessions(scanDate);

        sessions.Should().Equal(Day(2025, 6, 3));
    }

    [Fact]
    public async Task Previous_Session_Skips_The_Weekend()
    {
        var cache = new PreloadedMarketCache();
        var scanDate = Day(2025, 6, 9); // Monday
        Preload(cache, scanDate, Day(2025, 6, 6));
        var dataCache = new DataCache(cache, null!, NullLogger<DataCache>.Instance);

        var sessions = await dataCache.LoadPreviousMinuteSessions(scanDate);

        sessions.Should().Equal(Day(2025, 6, 6));
    }

    [Fact]
    public async Task Previous_Session_Skips_A_Holiday_Whose_File_Is_Missing()
    {
        // 2025-07-04 (Friday) is a market holiday: no per-day file. Scanning Monday 07-07 must
        // reach back to Thursday 07-03.
        var cache = new PreloadedMarketCache();
        var scanDate = Day(2025, 7, 7);
        Preload(cache, scanDate, Day(2025, 7, 3));
        var dataCache = new DataCache(cache, null!, NullLogger<DataCache>.Instance);

        var sessions = await dataCache.LoadPreviousMinuteSessions(scanDate);

        sessions.Should().Equal(Day(2025, 7, 3));
    }

    [Fact]
    public async Task No_Prior_File_Within_The_Lookback_Means_No_History_And_Setup_Still_Succeeds()
    {
        // First day in the bucket: nothing before it. The scan runs on same-day history only.
        var cache = new PreloadedMarketCache();
        var scanDate = Day(2025, 6, 4);
        Preload(cache, scanDate);
        var dataCache = new DataCache(cache, null!, NullLogger<DataCache>.Instance);

        (await dataCache.LoadPreviousMinuteSessions(scanDate)).Should().BeEmpty();
        (await dataCache.Setup(scanDate, [Minute])).Should().BeTrue();

        var history = dataCache.GetStocksResponse("SPY", Minute).Results;
        history.Select(b => b.Timestamp).Should().OnlyContain(t => t >= scanDate.ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task Setup_Prepends_The_Previous_Sessions_Minutes_Ahead_Of_The_Scan_Dates_PreMarket()
    {
        var cache = new PreloadedMarketCache();
        var scanDate = Day(2025, 6, 4);
        var previous = Day(2025, 6, 3);
        Preload(cache, scanDate, previous, Day(2025, 6, 2));
        var dataCache = new DataCache(cache, null!, NullLogger<DataCache>.Instance);

        (await dataCache.Setup(scanDate, [Minute])).Should().BeTrue();

        var history = dataCache.GetStocksResponse("SPY", Minute).Results;
        var open = scanDate.AddHours(9).AddMinutes(30).ToUnixTimeMilliseconds();
        history.Select(b => b.Timestamp).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        history.First().Timestamp.Should().Be(previous.AddHours(4).ToUnixTimeMilliseconds(), "the previous session's first pre-market minute leads the history");
        history.Last().Timestamp.Should().Be(open - 60_000, "history stops before the scan date's market open");
        history.Should().NotContain(b => b.Timestamp < previous.ToUnixTimeMilliseconds(), "only PreviousMinuteSessions (=1) sessions are prepended");

        // Intraday minutes are still served through the per-minute cursor, unaffected by the extra history.
        dataCache.HasNextCandle("SPY", 0, out var first).Should().BeTrue();
        first.Timestamp.Should().Be(open);
    }

    [Fact]
    public async Task Setup_Is_Idempotent_On_A_Warm_Container()
    {
        var cache = new PreloadedMarketCache();
        var scanDate = Day(2025, 6, 4);
        Preload(cache, scanDate, Day(2025, 6, 3));
        var dataCache = new DataCache(cache, null!, NullLogger<DataCache>.Instance);

        (await dataCache.Setup(scanDate, [Minute])).Should().BeTrue();
        var firstRun = dataCache.GetStocksResponse("SPY", Minute).Results.Select(b => b.Timestamp).ToList();
        (await dataCache.Setup(scanDate, [Minute])).Should().BeTrue();

        dataCache.GetStocksResponse("SPY", Minute).Results.Select(b => b.Timestamp).Should().Equal(firstRun);
    }

    [Fact]
    public async Task A_NonNotFound_S3_Failure_On_The_Previous_Session_Fails_Setup_Instead_Of_Silently_Dropping_History()
    {
        var cache = new ThrowingMarketCache(new PreloadedMarketCache(), Day(2025, 6, 3), new AmazonS3Exception("throttled") { StatusCode = HttpStatusCode.ServiceUnavailable });
        var scanDate = Day(2025, 6, 4);
        Preload(cache.Inner, scanDate, Day(2025, 6, 3));
        var dataCache = new DataCache(cache, null!, NullLogger<DataCache>.Instance);

        await dataCache.Invoking(d => d.LoadPreviousMinuteSessions(scanDate)).Should().ThrowAsync<AmazonS3Exception>();
        (await dataCache.Setup(scanDate, [Minute])).Should().BeFalse("the worker must fail the day loudly rather than scan with different history");
    }

    private static void Preload(PreloadedMarketCache cache, params DateTimeOffset[] dates)
    {
        foreach (var date in dates)
        {
            cache.Preload(Minute, date, MinuteFile("SPY", date));
        }
    }

    /// <summary>One bar per minute from 04:00 to 20:00 ET, i.e. a full extended-hours session.</summary>
    private static StocksResponse MinuteFile(string ticker, DateTimeOffset date)
    {
        var start = date.AddHours(4);
        var bars = new List<Bar>();
        for (var i = 0; i < 16 * 60; i++)
        {
            bars.Add(new Bar { Timestamp = start.AddMinutes(i).ToUnixTimeMilliseconds(), Open = 100, High = 101, Low = 99, Close = 100.5f, Volume = 1000 });
        }
        return new StocksResponse { Ticker = ticker, Status = "OK", Results = bars };
    }

    private sealed class ThrowingMarketCache(PreloadedMarketCache inner, DateTimeOffset failOn, Exception exception) : IMarketCache
    {
        public PreloadedMarketCache Inner => inner;

        public Task<IEnumerable<StocksResponse>> Initialize(DateTimeOffset date, Timeframe timeframe)
        {
            if (date.Date == failOn.Date && timeframe.Multiplier == 1 && timeframe.Timespan == Timespan.minute)
            {
                throw exception;
            }
            return inner.Initialize(date, timeframe);
        }

        public IEnumerable<string> GetTickers() => inner.GetTickers();
        public void SetTickers(IEnumerable<string> tickers) => inner.SetTickers(tickers);
        public IEnumerable<string> GetTickersByTimeframe(Timeframe timeframe, DateTimeOffset timestamp) => inner.GetTickersByTimeframe(timeframe, timestamp);
        public void SetTickersByTimeframe(DateTimeOffset date, Timeframe timeframe, IEnumerable<string> tickers) => inner.SetTickersByTimeframe(date, timeframe, tickers);
        public StocksResponse GetStocksResponse(string ticker, Timeframe timeframe, DateTimeOffset timestamp) => inner.GetStocksResponse(ticker, timeframe, timestamp);
        public void SetStocksResponse(StocksResponse stocksResponse, Timeframe timeframe, DateTimeOffset date) => inner.SetStocksResponse(stocksResponse, timeframe, date);
        public TickerDetails GetTickerDetails(string ticker) => inner.GetTickerDetails(ticker);
        public void SetTickerDetails(TickerDetails tickerDetails) => inner.SetTickerDetails(tickerDetails);
        public void AddLiveBar(MassiveWebsocketAggregateResponse bar) => inner.AddLiveBar(bar);
        public Bar GetLiveBar(string ticker) => inner.GetLiveBar(ticker);
        public IReadOnlyList<Bar> GetRecentLiveBars(string ticker) => inner.GetRecentLiveBars(ticker);
    }
}
