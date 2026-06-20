namespace StockMountain.MarketData;

public readonly record struct NormalizedBar(
    DateTimeOffset Timestamp,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume,
    decimal Vwap,
    int TransactionCount)
{
    public DateTimeOffset TimestampUtc => Timestamp.ToUniversalTime();
}
