using StockMountain.MarketData.Catalog;
using StockMountain.MarketData.Ingestion.Massive;
using StockMountain.MarketData.Storage;

namespace StockMountain.MarketData.Ingestion;

public sealed class BackfillPipeline
{
    private readonly IHistoricalBarFetcher _fetcher;
    private readonly MonthlyBarFileWriter _monthlyWriter;

    public BackfillPipeline(IHistoricalBarFetcher fetcher, MonthlyBarFileWriter monthlyWriter)
    {
        _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
        _monthlyWriter = monthlyWriter ?? throw new ArgumentNullException(nameof(monthlyWriter));
    }

    public async Task<IReadOnlyList<BarSeriesFile>> RunAsync(
        BackfillRequest request,
        CancellationToken cancellationToken = default)
    {
        request.Validate();
        MassiveBarFetcher.EnsureSupportedSeries(request.Series);

        var fetchedBars = await _fetcher.FetchAsync(request.Series, request.From, request.To, cancellationToken);
        var clippedBars = fetchedBars
            .Where(bar => bar.TimestampUtc >= request.From.ToUniversalTime()
                && bar.TimestampUtc <= request.To.ToUniversalTime())
            .ToArray();

        if (clippedBars.Length == 0)
        {
            return [];
        }

        var writtenFiles = new List<BarSeriesFile>();
        foreach (var monthGroup in clippedBars.GroupBy(static bar => (bar.TimestampUtc.Year, bar.TimestampUtc.Month)))
        {
            var file = await _monthlyWriter.WriteMonthAsync(
                request.Series,
                monthGroup.Key.Year,
                monthGroup.Key.Month,
                monthGroup.ToArray(),
                cancellationToken);

            writtenFiles.Add(file);
        }

        return writtenFiles;
    }
}
