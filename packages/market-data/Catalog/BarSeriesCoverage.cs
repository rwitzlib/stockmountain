namespace StockMountain.MarketData.Catalog;

public sealed record BarSeriesCoverage(
    BarSeriesKey Series,
    DateTimeOffset EarliestBarStart,
    DateTimeOffset LatestBarStart)
{
    public void Validate()
    {
        Series.Validate();

        if (LatestBarStart < EarliestBarStart)
        {
            throw new ArgumentException("LatestBarStart must not be before EarliestBarStart.", nameof(LatestBarStart));
        }
    }
}
