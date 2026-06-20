using StockMountain.MarketData;
using StockMountain.MarketData.Catalog;
using StockMountain.MarketData.Reading;
using StockMountain.MarketData.Storage;

namespace StockMountain.MarketData.Tests;

public class BarSeriesReaderTests
{
    [Fact]
    public async Task ReadAvailableAsync_ReturnsBarsAcrossMonthlyFilesInOrder()
    {
        var series = new BarSeriesKey(
            Symbol.Create("AAPL"),
            new Timeframe(1, TimeframeUnit.Day),
            AdjustmentPolicy.SplitAdjusted);

        var catalog = new InMemoryBarSeriesCatalog();
        var objectStore = new InMemoryBarObjectStore();
        var writer = new MonthlyBarFileWriter(objectStore, catalog);

        await writer.WriteMonthAsync(series, 2024, 1,
        [
            new NormalizedBar(new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero), 1, 2, 0.5m, 1.5m, 100, 1.2m, 10),
        ]);
        await writer.WriteMonthAsync(series, 2024, 2,
        [
            new NormalizedBar(new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero), 2, 3, 1.5m, 2.5m, 200, 2.2m, 20),
        ]);

        var reader = new BarSeriesReader(catalog, objectStore);
        var bars = new List<NormalizedBar>();
        await foreach (var bar in reader.ReadAvailableAsync(
                           series,
                           new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero),
                           new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero)))
        {
            bars.Add(bar);
        }

        Assert.Equal(2, bars.Count);
        Assert.Equal(new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero), bars[0].TimestampUtc);
        Assert.Equal(new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero), bars[1].TimestampUtc);
    }

    [Fact]
    public async Task ReadAsync_ThrowsWhenRequestedRangeHasGap()
    {
        var series = new BarSeriesKey(
            Symbol.Create("AAPL"),
            new Timeframe(1, TimeframeUnit.Day),
            AdjustmentPolicy.SplitAdjusted);

        var catalog = new InMemoryBarSeriesCatalog();
        var objectStore = new InMemoryBarObjectStore();
        var writer = new MonthlyBarFileWriter(objectStore, catalog);

        await writer.WriteMonthAsync(series, 2024, 1,
        [
            new NormalizedBar(new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero), 1, 2, 0.5m, 1.5m, 100, 1.2m, 10),
        ]);

        var reader = new BarSeriesReader(catalog, objectStore);

        var exception = await Assert.ThrowsAsync<BarSeriesCoverageGapException>(async () =>
        {
            await foreach (var _ in reader.ReadAsync(
                               series,
                               new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                               new DateTimeOffset(2024, 1, 3, 0, 0, 0, TimeSpan.Zero)))
            {
            }
        });

        Assert.NotEmpty(exception.Gaps);
    }

    private sealed class InMemoryBarSeriesCatalog : IBarSeriesCatalog
    {
        private readonly Dictionary<string, BarSeriesFile> _files = new(StringComparer.Ordinal);

        public Task<IReadOnlyList<BarSeriesFile>> GetFilesAsync(
            BarSeriesKey series,
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default)
        {
            var files = _files.Values
                .Where(file => file.Series == series
                    && file.EarliestBarStart <= to
                    && file.LatestBarStart >= from)
                .OrderBy(file => file.EarliestBarStart)
                .ToArray();

            return Task.FromResult<IReadOnlyList<BarSeriesFile>>(files);
        }

        public Task<BarSeriesCoverage?> GetCoverageAsync(
            BarSeriesKey series,
            CancellationToken cancellationToken = default)
        {
            var files = _files.Values.Where(file => file.Series == series).ToArray();
            if (files.Length == 0)
            {
                return Task.FromResult<BarSeriesCoverage?>(null);
            }

            return Task.FromResult<BarSeriesCoverage?>(new BarSeriesCoverage(
                series,
                files.Min(file => file.EarliestBarStart),
                files.Max(file => file.LatestBarStart)));
        }

        public Task RegisterFileAsync(BarSeriesFile file, CancellationToken cancellationToken = default)
        {
            _files[file.ObjectKey] = file;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DateRange>> FindGapsAsync(
            BarSeriesKey series,
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default)
        {
            var files = _files.Values
                .Where(file => file.Series == series
                    && file.EarliestBarStart <= to
                    && file.LatestBarStart >= from)
                .OrderBy(file => file.EarliestBarStart)
                .ToList();

            if (files.Count == 0)
            {
                return Task.FromResult<IReadOnlyList<DateRange>>([new DateRange(from, to)]);
            }

            var gaps = new List<DateRange>();
            var cursor = from;

            foreach (var file in files)
            {
                if (file.EarliestBarStart > cursor)
                {
                    gaps.Add(new DateRange(cursor, file.EarliestBarStart <= to ? file.EarliestBarStart : to));
                }

                cursor = cursor >= file.LatestBarStart ? cursor : file.LatestBarStart.AddTicks(1);
                if (cursor >= to)
                {
                    return Task.FromResult<IReadOnlyList<DateRange>>(gaps);
                }
            }

            if (cursor < to)
            {
                gaps.Add(new DateRange(cursor, to));
            }

            return Task.FromResult<IReadOnlyList<DateRange>>(gaps);
        }
    }
}
