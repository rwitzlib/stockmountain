namespace StockMountain.MarketData.Catalog;

public sealed record BarSeriesFile(
    BarSeriesKey Series,
    string ObjectKey,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    DateTimeOffset EarliestBarStart,
    DateTimeOffset LatestBarStart,
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

        if (LatestBarStart < EarliestBarStart)
        {
            throw new ArgumentException("LatestBarStart must not be before EarliestBarStart.", nameof(LatestBarStart));
        }

        if (BarCount is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BarCount), BarCount, "BarCount cannot be negative.");
        }
    }

    public static BarSeriesFile FromBars(
        BarSeriesKey series,
        string objectKey,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        IReadOnlyList<NormalizedBar> bars)
    {
        ArgumentNullException.ThrowIfNull(bars);

        if (bars.Count == 0)
        {
            throw new ArgumentException("At least one bar is required.", nameof(bars));
        }

        var earliest = bars[0].TimestampUtc;
        var latest = bars[0].TimestampUtc;

        for (var i = 1; i < bars.Count; i++)
        {
            var timestamp = bars[i].TimestampUtc;
            if (timestamp < earliest)
            {
                earliest = timestamp;
            }

            if (timestamp > latest)
            {
                latest = timestamp;
            }
        }

        return new BarSeriesFile(series, objectKey, periodStart, periodEnd, earliest, latest, bars.Count);
    }
}
