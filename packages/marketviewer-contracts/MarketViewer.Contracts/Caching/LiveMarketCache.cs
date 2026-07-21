using Amazon.S3;
using Microsoft.Extensions.Caching.Memory;

namespace MarketViewer.Contracts.Caching;

/// <summary>
/// Market cache for long-running live services (API, bots). Holds a single rolling
/// window of data per ticker/timeframe, so keys ignore the calendar date — a
/// date-keyed cache goes dark as soon as the server's date rolls past the date it
/// was warmed with. The backtester keeps using MemoryMarketCache because it needs
/// multiple distinct dates cached side by side.
/// </summary>
public class LiveMarketCache(IMemoryCache memoryCache, IAmazonS3 s3) : MemoryMarketCache(memoryCache, s3)
{
    protected override string DateKey(DateTimeOffset timestamp) => "live";

    /// <summary>
    /// Overwrite with no expiration: the daily re-warm replaces entries wholesale,
    /// which both bounds staleness and prevents idle entries (weekends, holidays)
    /// from silently expiring with nothing left to repopulate them.
    /// </summary>
    protected override void SetAggregateEntry<T>(string key, T value)
    {
        memoryCache.Set(key, value);
    }
}
