using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using Massive.Client.Models;

namespace MarketViewer.Api.Services;

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
    public Bar AddBarToCache(string ticker, Timeframe timeframe, Bar newCandle)
    {
        try
        {
            var stocksResponse = marketCache.GetStocksResponse(ticker, timeframe, DateTimeOffset.Now);

            if (stocksResponse is null || !stocksResponse.Results.Any())
            {
                return null;
            }

            var lastCandle = stocksResponse.Results.Last();

            if (lastCandle.Timestamp >= newCandle.Timestamp)
            {
                return null;
            }

            switch (timeframe.Timespan)
            {
                case Timespan.minute:
                    stocksResponse.Results.Add(newCandle.Clone());
                    return newCandle.Clone();

                case Timespan.hour:
                    var lastDateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(lastCandle.Timestamp);
                    var currentDateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(newCandle.Timestamp);

                    if (lastDateTimeOffset.Hour < currentDateTimeOffset.Hour)
                    {
                        stocksResponse.Results.Add(newCandle.Clone());
                    }
                    else
                    {
                        if (newCandle.Volume == lastCandle.Volume && newCandle.TransactionCount == lastCandle.TransactionCount)
                        {
                            // There hasn't been a new candle yet so don't update
                            return null;
                        }

                        if (newCandle.High > lastCandle.High)
                        {
                            lastCandle.High = newCandle.High;
                        }

                        if (newCandle.Low < lastCandle.Low)
                        {
                            lastCandle.Low = newCandle.Low;
                        }

                        lastCandle.Close = newCandle.Close;

                        // TODO: How to do a more precise VWAP?
                        lastCandle.Vwap = (newCandle.Close + newCandle.High + newCandle.Low) / 3;

                        lastCandle.Volume += newCandle.Volume;
                        lastCandle.TransactionCount += newCandle.TransactionCount;
                    }
                    return lastCandle;

                default:
                    if (newCandle.Volume == lastCandle.Volume && newCandle.TransactionCount == lastCandle.TransactionCount)
                    {
                        // There hasn't been a new candle yet so don't update
                        return null;
                    }

                    if (newCandle.High > lastCandle.High)
                    {
                        lastCandle.High = newCandle.High;
                    }

                    if (newCandle.Low < lastCandle.Low)
                    {
                        lastCandle.Low = newCandle.Low;
                    }

                    lastCandle.Close = newCandle.Close;

                    // TODO: How to do a more precise VWAP?
                    lastCandle.Vwap = (newCandle.Close + newCandle.High + newCandle.Low) / 3;

                    lastCandle.Volume += newCandle.Volume;
                    lastCandle.TransactionCount += newCandle.TransactionCount;
                    return lastCandle;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding bar to cache for {ticker} at timeframe {timeframe}", ticker, timeframe);
            return null;
        }
    }
}
