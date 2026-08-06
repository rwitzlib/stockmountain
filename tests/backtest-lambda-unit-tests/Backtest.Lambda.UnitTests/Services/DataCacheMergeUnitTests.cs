using Backtest.Lambda.Services;
using FluentAssertions;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market;
using Massive.Client.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Timespan = MarketViewer.Contracts.Enums.Timespan;

namespace Backtest.Lambda.UnitTests.Services;

public class DataCacheMergeUnitTests
{
    private static readonly Timeframe Day = new(1, Timespan.day);
    private static readonly DateTimeOffset Date = DateTimeOffset.Parse("2026-01-12T00:00:00-05:00");
    private static readonly DateTimeOffset PreviousDate = Date.AddYears(-1);

    private readonly IMarketCache _marketCache;
    private readonly DataCache _classUnderTest;

    public DataCacheMergeUnitTests()
    {
        _marketCache = new MemoryMarketCache(new MemoryCache(new MemoryCacheOptions()), null);
        _classUnderTest = new DataCache(_marketCache, null, NullLogger<DataCache>.Instance);
    }

    /// <summary>
    /// Yearly aggregate files overlap by one bar: the current year's file starts with the
    /// previous year's final session (e.g. the 2026 file's first bar is 2025-12-31, which
    /// is also the 2025 file's last bar). The merge must dedupe that bar, not treat the
    /// overlap as "already merged" and skip prepending the previous year entirely.
    /// </summary>
    [Fact]
    public void Merge_Prepends_Previous_Year_When_Files_Overlap_By_One_Bar()
    {
        var previous = MakeResponse("SPY", BarsEndingAt(new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.FromHours(-5)), 251));
        var current = MakeResponse("SPY", BarsStartingAt(new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.FromHours(-5)), 8));
        Seed(previous, current);

        _classUnderTest.MergePreviousPeriod(Day, Date, PreviousDate);

        var merged = _marketCache.GetStocksResponse("SPY", Day, Date);
        merged.Results.Should().HaveCount(251 + 8 - 1);
        merged.Results.Select(bar => bar.Timestamp).Should().BeInAscendingOrder();
        merged.Results.Select(bar => bar.Timestamp).Should().OnlyHaveUniqueItems();
        merged.Results.First().Timestamp.Should().Be(previous.Results.First().Timestamp);
        merged.Results.Last().Timestamp.Should().Be(current.Results.Last().Timestamp);
    }

    [Fact]
    public void Merge_Is_Idempotent_Across_Repeated_Setup_Calls()
    {
        var previous = MakeResponse("SPY", BarsEndingAt(new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.FromHours(-5)), 251));
        var current = MakeResponse("SPY", BarsStartingAt(new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.FromHours(-5)), 8));
        Seed(previous, current);

        _classUnderTest.MergePreviousPeriod(Day, Date, PreviousDate);
        _classUnderTest.MergePreviousPeriod(Day, Date, PreviousDate);

        var merged = _marketCache.GetStocksResponse("SPY", Day, Date);
        merged.Results.Should().HaveCount(251 + 8 - 1);
        merged.Results.Select(bar => bar.Timestamp).Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// A ticker that did not trade on the previous year's final session has no
    /// overlapping bar; every previous bar should still be prepended.
    /// </summary>
    [Fact]
    public void Merge_Prepends_All_Previous_Bars_When_There_Is_No_Overlap()
    {
        var previous = MakeResponse("ATGL", BarsEndingAt(new DateTimeOffset(2025, 12, 30, 0, 0, 0, TimeSpan.FromHours(-5)), 229));
        var current = MakeResponse("ATGL", BarsStartingAt(new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.FromHours(-5)), 6));
        Seed(previous, current);

        _classUnderTest.MergePreviousPeriod(Day, Date, PreviousDate);

        var merged = _marketCache.GetStocksResponse("ATGL", Day, Date);
        merged.Results.Should().HaveCount(229 + 6);
        merged.Results.Select(bar => bar.Timestamp).Should().BeInAscendingOrder();
    }

    [Fact]
    public void Merge_Skips_Ticker_Missing_From_Previous_Year()
    {
        var current = MakeResponse("NEWIPO", BarsStartingAt(new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.FromHours(-5)), 6));
        _marketCache.SetTickersByTimeframe(Date, Day, [current.Ticker]);
        _marketCache.SetStocksResponse(current, Day, Date);

        _classUnderTest.MergePreviousPeriod(Day, Date, PreviousDate);

        var merged = _marketCache.GetStocksResponse("NEWIPO", Day, Date);
        merged.Results.Should().HaveCount(6);
    }

    private void Seed(StocksResponse previous, StocksResponse current)
    {
        _marketCache.SetTickersByTimeframe(Date, Day, [current.Ticker]);
        _marketCache.SetStocksResponse(current, Day, Date);
        _marketCache.SetStocksResponse(previous, Day, PreviousDate);
    }

    private static StocksResponse MakeResponse(string ticker, List<Bar> bars) => new()
    {
        Ticker = ticker,
        Results = bars
    };

    private static List<Bar> BarsEndingAt(DateTimeOffset end, int count)
    {
        return BarsStartingAt(end.AddDays(-(count - 1)), count);
    }

    private static List<Bar> BarsStartingAt(DateTimeOffset start, int count)
    {
        var bars = new List<Bar>(count);
        for (var i = 0; i < count; i++)
        {
            bars.Add(new Bar
            {
                Timestamp = start.AddDays(i).ToUnixTimeMilliseconds(),
                Open = 100,
                High = 101,
                Low = 99,
                Close = 100.5f,
                Volume = 1000
            });
        }
        return bars;
    }
}
