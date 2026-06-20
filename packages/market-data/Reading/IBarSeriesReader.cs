namespace StockMountain.MarketData.Reading;

public interface IBarSeriesReader
{
    IAsyncEnumerable<NormalizedBar> ReadAvailableAsync(
        BarSeriesKey series,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<NormalizedBar> ReadAsync(
        BarSeriesKey series,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}
