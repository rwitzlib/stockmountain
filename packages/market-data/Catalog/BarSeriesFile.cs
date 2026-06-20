namespace StockMountain.MarketData.Catalog;

public sealed record BarSeriesFile(
    BarSeriesKey Series,
    string ObjectKey,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    long? BarCount = null)
{
    public void Validate()
    {
        Series.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(ObjectKey);

        if (PeriodEnd <= PeriodStart)
        {
            throw new ArgumentException("PeriodEnd must be after PeriodStart.", nameof(PeriodEnd));
        }

        if (BarCount is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BarCount), BarCount, "BarCount cannot be negative.");
        }
    }
}
