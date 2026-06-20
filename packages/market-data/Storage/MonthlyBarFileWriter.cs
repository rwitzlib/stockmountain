using StockMountain.MarketData.Catalog;

namespace StockMountain.MarketData.Storage;

public sealed class MonthlyBarFileWriter
{
    private readonly IBarObjectStore _objectStore;
    private readonly IBarSeriesCatalog _catalog;

    public MonthlyBarFileWriter(IBarObjectStore objectStore, IBarSeriesCatalog catalog)
    {
        _objectStore = objectStore ?? throw new ArgumentNullException(nameof(objectStore));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public async Task<BarSeriesFile> WriteMonthAsync(
        BarSeriesKey series,
        int year,
        int month,
        IReadOnlyList<NormalizedBar> incomingBars,
        CancellationToken cancellationToken = default)
    {
        series.Validate();
        ArgumentNullException.ThrowIfNull(incomingBars);

        if (incomingBars.Count == 0)
        {
            throw new ArgumentException("At least one bar is required.", nameof(incomingBars));
        }

        var objectKey = BarObjectKey.ForMonth(series, year, month);
        var existingBars = await ReadExistingBarsAsync(objectKey, cancellationToken);
        var mergedBars = NormalizedBarMerger.Merge(existingBars, incomingBars);

        await using var uploadStream = new MemoryStream();
        await NormalizedBarParquetWriter.WriteAsync(uploadStream, mergedBars, cancellationToken);
        uploadStream.Position = 0;
        await _objectStore.WriteAsync(objectKey, uploadStream, cancellationToken);

        var file = BarSeriesFile.FromBars(
            series,
            objectKey,
            BarObjectKey.MonthPeriodStartUtc(year, month),
            BarObjectKey.MonthPeriodEndUtc(year, month),
            mergedBars);

        await _catalog.RegisterFileAsync(file, cancellationToken);
        return file;
    }

    private async Task<IReadOnlyList<NormalizedBar>> ReadExistingBarsAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        if (!await _objectStore.ExistsAsync(objectKey, cancellationToken))
        {
            return [];
        }

        await using var stream = await _objectStore.OpenReadAsync(objectKey, cancellationToken);
        return await NormalizedBarParquetReader.ReadAllAsync(stream, cancellationToken);
    }
}
