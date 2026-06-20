namespace StockMountain.MarketData.Ingestion;

public interface IHistoricalBarFetcher
{
    Task<IReadOnlyList<NormalizedBar>> FetchAsync(
        BarSeriesKey series,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}
