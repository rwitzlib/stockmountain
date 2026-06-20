namespace StockMountain.MarketData.Catalog;

public interface IBarSeriesCatalog
{
    Task<IReadOnlyList<BarSeriesFile>> GetFilesAsync(
        BarSeriesKey series,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<BarSeriesCoverage?> GetCoverageAsync(
        BarSeriesKey series,
        CancellationToken cancellationToken = default);

    Task RegisterFileAsync(
        BarSeriesFile file,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DateRange>> FindGapsAsync(
        BarSeriesKey series,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}
