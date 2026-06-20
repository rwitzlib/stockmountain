namespace StockMountain.MarketData.Ingestion.Massive;

public static class MassiveAggregateBarNormalizer
{
    public static NormalizedBar ToNormalizedBar(MassiveAggregateBar aggregate, Timeframe timeframe)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        timeframe.Validate();

        var timestamp = NormalizeTimestamp(
            DateTimeOffset.FromUnixTimeMilliseconds(aggregate.TimestampMilliseconds).ToUniversalTime(),
            timeframe);

        return new NormalizedBar(
            timestamp,
            aggregate.Open,
            aggregate.High,
            aggregate.Low,
            aggregate.Close,
            aggregate.Volume,
            aggregate.Vwap,
            aggregate.TransactionCount);
    }

    internal static DateTimeOffset NormalizeTimestamp(DateTimeOffset timestampUtc, Timeframe timeframe)
    {
        if (timeframe.Unit == TimeframeUnit.Minute && timeframe.Multiplier == 1)
        {
            return new DateTimeOffset(
                timestampUtc.Year,
                timestampUtc.Month,
                timestampUtc.Day,
                timestampUtc.Hour,
                timestampUtc.Minute,
                0,
                TimeSpan.Zero);
        }

        if (timeframe.Unit == TimeframeUnit.Minute)
        {
            return new DateTimeOffset(
                timestampUtc.Year,
                timestampUtc.Month,
                timestampUtc.Day,
                timestampUtc.Hour,
                timestampUtc.Minute,
                timestampUtc.Second,
                TimeSpan.Zero);
        }

        return timestampUtc;
    }
}
