using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using Massive.Client.Models;

namespace MarketViewer.Api.Services;

/// <summary>
/// Outcome of adding a bar to the cache. A gap is reported when a minute bar was
/// appended more than one minute after the previous bar — the skipped minutes were
/// never cached (missed snapshot poll, or the ticker simply didn't trade) and the
/// caller decides whether to backfill.
/// </summary>
public readonly record struct BarCacheResult(Bar Added, long? GapFromTimestamp, long? GapToTimestamp)
{
    public bool HasGap => GapFromTimestamp is not null;
}

/// <summary>
/// Shared service for adding bars to the market cache.
/// Used by both CacheWarmupService and SnapshotJob.
/// </summary>
public class BarCacheService(IMarketCache marketCache, ILogger<BarCacheService> logger)
{
    /// <summary>
    /// Adds a new bar to the cache for the specified ticker and timeframe.
    /// Handles merging logic for hourly candles.
    /// </summary>
    public BarCacheResult AddBarToCache(string ticker, Timeframe timeframe, Bar newCandle)
    {
        try
        {
            var stocksResponse = marketCache.GetStocksResponse(ticker, timeframe, DateTimeOffset.Now);

            if (stocksResponse?.Results is not { Count: > 0 })
            {
                return default;
            }

            var lastCandle = stocksResponse.Results.Last();

            if (lastCandle.Timestamp >= newCandle.Timestamp)
            {
                return default;
            }

            switch (timeframe.Timespan)
            {
                case Timespan.minute:
                    long? gapFrom = null;
                    long? gapTo = null;
                    if (newCandle.Timestamp - lastCandle.Timestamp > 60_000)
                    {
                        gapFrom = lastCandle.Timestamp;
                        gapTo = newCandle.Timestamp;
                    }

                    var added = newCandle.Clone();
                    stocksResponse.Results.Add(added);
                    return new BarCacheResult(added, gapFrom, gapTo);

                case Timespan.hour:
                    if (newCandle.Timestamp / 3_600_000 > lastCandle.Timestamp / 3_600_000)
                    {
                        var addedHour = newCandle.Clone();
                        stocksResponse.Results.Add(addedHour);
                        return new BarCacheResult(addedHour, null, null);
                    }

                    if (newCandle.Volume == lastCandle.Volume && newCandle.TransactionCount == lastCandle.TransactionCount)
                    {
                        // There hasn't been a new candle yet so don't update
                        return default;
                    }

                    MergeIntoLastCandle(lastCandle, newCandle);
                    return new BarCacheResult(lastCandle, null, null);

                default:
                    if (newCandle.Volume == lastCandle.Volume && newCandle.TransactionCount == lastCandle.TransactionCount)
                    {
                        // There hasn't been a new candle yet so don't update
                        return default;
                    }

                    MergeIntoLastCandle(lastCandle, newCandle);
                    return new BarCacheResult(lastCandle, null, null);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding bar to cache for {ticker} at timeframe {timeframe}", ticker, timeframe);
            return default;
        }
    }

    /// <summary>
    /// Splices completed minute bars into a previously reported gap (exclusive
    /// bounds), keeping the cached series sorted and skipping minutes already
    /// present. Returns how many bars were inserted.
    /// </summary>
    public int BackfillMinuteBars(string ticker, IEnumerable<Bar> bars, long gapFromExclusive, long gapToExclusive)
    {
        try
        {
            var stocksResponse = marketCache.GetStocksResponse(ticker, new Timeframe(1, Timespan.minute), DateTimeOffset.Now);

            if (stocksResponse?.Results is not { Count: > 0 })
            {
                return 0;
            }

            var results = stocksResponse.Results;
            var inserted = 0;

            var candidates = bars
                .Where(bar => bar.Timestamp > gapFromExclusive && bar.Timestamp < gapToExclusive)
                .OrderBy(bar => bar.Timestamp);

            foreach (var bar in candidates)
            {
                // Gaps sit near the tail of the series, so search from the end.
                var index = results.FindLastIndex(q => q.Timestamp < bar.Timestamp);

                if (index < 0 || (index < results.Count - 1 && results[index + 1].Timestamp <= bar.Timestamp))
                {
                    continue; // out of range or that minute already exists
                }

                results.Insert(index + 1, bar.Clone());
                inserted++;
            }

            return inserted;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error backfilling minute bars for {ticker}", ticker);
            return 0;
        }
    }

    private static void MergeIntoLastCandle(Bar lastCandle, Bar newCandle)
    {
        if (newCandle.High > lastCandle.High)
        {
            lastCandle.High = newCandle.High;
        }

        if (newCandle.Low < lastCandle.Low)
        {
            lastCandle.Low = newCandle.Low;
        }

        lastCandle.Close = newCandle.Close;

        // Volume-weighted, from the merged minutes' vw (must run before Volume is summed).
        lastCandle.Vwap = BarVwap.Merge(lastCandle, newCandle, BarVwap.TypicalPrice(lastCandle));

        lastCandle.Volume += newCandle.Volume;
        lastCandle.TransactionCount += newCandle.TransactionCount;
    }
}
