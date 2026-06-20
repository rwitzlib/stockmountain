using StockMountain.MarketData;
using StockMountain.MarketData.Catalog;
using StockMountain.MarketData.Ingestion;
using StockMountain.MarketData.Storage;

namespace StockMountain.MarketData.Ingestion.Tests;

public class BackfillPipelineTests
{
    [Fact]
    public async Task RunAsync_MergesIntoMonthlyFileAndRegistersCatalog()
    {
        var series = new BarSeriesKey(
            Symbol.Create("AAPL"),
            new Timeframe(1, TimeframeUnit.Day),
            AdjustmentPolicy.SplitAdjusted);

        var catalog = new InMemoryBarSeriesCatalog();
        var objectStore = new InMemoryBarObjectStore();
        var writer = new MonthlyBarFileWriter(objectStore, catalog);
        var fetcher = new StubFetcher([
            new NormalizedBar(new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero), 1, 2, 0.5m, 1.5m, 100, 1.2m, 10),
            new NormalizedBar(new DateTimeOffset(2024, 1, 3, 0, 0, 0, TimeSpan.Zero), 2, 3, 1.5m, 2.5m, 200, 2.2m, 20),
        ]);

        var pipeline = new BackfillPipeline(fetcher, writer);
        var files = await pipeline.RunAsync(new BackfillRequest(
            series,
            new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 1, 3, 23, 59, 59, TimeSpan.Zero)));

        Assert.Single(files);
        Assert.Equal("bars/AAPL/1d/split-adjusted/2024/01.parquet", files[0].ObjectKey);
        Assert.Equal(2, files[0].BarCount);
        Assert.Equal(new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero), files[0].EarliestBarStart);
        Assert.Equal(new DateTimeOffset(2024, 1, 3, 0, 0, 0, TimeSpan.Zero), files[0].LatestBarStart);

        await using var stream = await objectStore.OpenReadAsync(files[0].ObjectKey);
        var storedBars = await NormalizedBarParquetReader.ReadAllAsync(stream);
        Assert.Equal(2, storedBars.Count);

        var secondRunFetcher = new StubFetcher([
            new NormalizedBar(new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero), 9, 9, 9, 9, 9, 9, 9),
        ]);
        var secondPipeline = new BackfillPipeline(secondRunFetcher, writer);
        var updatedFiles = await secondPipeline.RunAsync(new BackfillRequest(
            series,
            new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 1, 2, 23, 59, 59, TimeSpan.Zero)));

        Assert.Single(updatedFiles);
        await using var updatedStream = await objectStore.OpenReadAsync(updatedFiles[0].ObjectKey);
        var mergedBars = await NormalizedBarParquetReader.ReadAllAsync(updatedStream);
        Assert.Equal(2, mergedBars.Count);
        Assert.Equal(9m, mergedBars[0].Open);
    }

    private sealed class StubFetcher(IReadOnlyList<NormalizedBar> bars) : IHistoricalBarFetcher
    {
        public Task<IReadOnlyList<NormalizedBar>> FetchAsync(
            BarSeriesKey series,
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(bars);
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
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DateRange>>([]);
    }
}
