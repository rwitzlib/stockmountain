namespace StockMountain.MarketData.Ingestion;

public sealed record BackfillRequest(
    BarSeriesKey Series,
    DateTimeOffset From,
    DateTimeOffset To)
{
    public void Validate()
    {
        Series.Validate();

        if (To <= From)
        {
            throw new ArgumentException("To must be after From.", nameof(To));
        }
    }
}
