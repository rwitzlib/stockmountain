namespace StockMountain.MarketData.Catalog;

public readonly record struct DateRange(DateTimeOffset Start, DateTimeOffset End)
{
    public void Validate()
    {
        if (End <= Start)
        {
            throw new ArgumentException("End must be after Start.", nameof(End));
        }
    }
}
