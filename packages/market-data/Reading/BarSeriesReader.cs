using StockMountain.MarketData.Catalog;
using StockMountain.MarketData.Storage;

namespace StockMountain.MarketData.Reading;

public sealed class BarSeriesReader : IBarSeriesReader
{
    private readonly IBarSeriesCatalog _catalog;
    private readonly IBarObjectStore _objectStore;

    public BarSeriesReader(IBarSeriesCatalog catalog, IBarObjectStore objectStore)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _objectStore = objectStore ?? throw new ArgumentNullException(nameof(objectStore));
    }

    public async IAsyncEnumerable<NormalizedBar> ReadAsync(
        BarSeriesKey series,
        DateTimeOffset from,
        DateTimeOffset to,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var gaps = await _catalog.FindGapsAsync(series, from, to, cancellationToken);
        if (gaps.Count > 0)
        {
            throw new BarSeriesCoverageGapException(gaps);
        }

        await foreach (var bar in ReadAvailableAsync(series, from, to, cancellationToken))
        {
            yield return bar;
        }
    }

    public async IAsyncEnumerable<NormalizedBar> ReadAvailableAsync(
        BarSeriesKey series,
        DateTimeOffset from,
        DateTimeOffset to,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        series.Validate();

        if (to < from)
        {
            throw new ArgumentException("to must not be before from.", nameof(to));
        }

        var files = await _catalog.GetFilesAsync(series, from, to, cancellationToken);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var stream = await _objectStore.OpenReadAsync(file.ObjectKey, cancellationToken);
            await foreach (var bar in NormalizedBarParquetReader.ReadRangeAsync(stream, from, to, cancellationToken))
            {
                yield return bar;
            }
        }
    }
}
