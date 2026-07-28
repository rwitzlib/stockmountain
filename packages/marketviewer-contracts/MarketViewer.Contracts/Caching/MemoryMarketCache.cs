using Amazon.S3;
using Amazon.S3.Model;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.MarketData;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Responses.Market;
using Microsoft.Extensions.Caching.Memory;
using Massive.Client.Models;
using Massive.Client.Responses;
using System.Text.Json;

namespace MarketViewer.Contracts.Caching;

public class MemoryMarketCache(IMemoryCache memoryCache, IAmazonS3 s3) : IMarketCache
{
    private readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static TimeSpan ExpireIn => TimeSpan.FromHours(16);

    /// <summary>
    /// Date segment used in aggregate cache keys. The backtester caches multiple
    /// distinct dates side by side; LiveMarketCache overrides this so the live API
    /// cache survives the date rolling over mid-run.
    /// </summary>
    protected virtual string DateKey(DateTimeOffset timestamp) => timestamp.Date.ToString("yyyyMMdd");

    /// <summary>
    /// Writes an aggregate entry. The backtester relies on create-only semantics
    /// (warm Lambdas must not re-download over existing data) with a sliding
    /// expiration; LiveMarketCache overrides to overwrite so its daily re-warm can
    /// replace entries wholesale.
    /// </summary>
    protected virtual void SetAggregateEntry<T>(string key, T value)
    {
        memoryCache.GetOrCreate(key, entry =>
        {
            entry.SetSlidingExpiration(ExpireIn);
            return value;
        });
    }

    
    public async Task<IEnumerable<StocksResponse>> Initialize(DateTimeOffset date, Timeframe timeframe)
    {
        var s3Request = new GetObjectRequest
        {
            BucketName = Environment.GetEnvironmentVariable("MARKET_DATA_BUCKET_NAME") ?? MarketDataStorageContract.DefaultBucketName,
            Key = MarketDataStorageContract.BuildAggregateKey(date, timeframe.Multiplier, timeframe.Timespan)
        };

        using var s3Response = await s3.GetObjectAsync(s3Request);

        // Deserialize straight from the S3 stream; materializing the file as a
        // string first doubles it (UTF-16) on the Large Object Heap.
        var stocksResponses = await JsonSerializer.DeserializeAsync<List<StocksResponse>>(s3Response.ResponseStream, Options);

        var tickers = stocksResponses.Select(stocksResponse => stocksResponse.Ticker);

        SetTickersByTimeframe(date, timeframe, tickers); //TODO use multiplier in cache key eventually?

        foreach (var stocksResponse in stocksResponses)
        {
            SetStocksResponse(stocksResponse, timeframe, date); //TODO use multiplier in cache key eventually?
        }

        return stocksResponses;
    }

    public IEnumerable<string> GetTickers()
    {
        return memoryCache.Get<IEnumerable<string>>("Tickers");
    }

    public void SetTickers(IEnumerable<string> tickers)
    {
        memoryCache.Set("Tickers", tickers);
    }

    public IEnumerable<string> GetTickersByTimeframe(Timeframe timeframe, DateTimeOffset timestamp)
    {
        return memoryCache.Get<IEnumerable<string>>($"Tickers/{timeframe.Multiplier}/{timeframe.Timespan}/{DateKey(timestamp)}");
    }

    public void SetTickersByTimeframe(DateTimeOffset date, Timeframe timeframe, IEnumerable<string> tickers)
    {
        SetAggregateEntry($"Tickers/{timeframe.Multiplier}/{timeframe.Timespan}/{DateKey(date)}", tickers);
    }

    public StocksResponse GetStocksResponse(string ticker, Timeframe timeframe, DateTimeOffset timestamp)
    {
        if (string.IsNullOrWhiteSpace(ticker) || timeframe is null)
        {
            return null;
        }
        return memoryCache.Get<StocksResponse>($"Stocks/{ticker}/{timeframe.Multiplier}/{timeframe.Timespan}/{DateKey(timestamp)}");
    }

    public void SetStocksResponse(StocksResponse stocksResponse, Timeframe timeframe, DateTimeOffset date)
    {
        if (stocksResponse is null)
        {
            return;
        }

        SetAggregateEntry($"Stocks/{stocksResponse.Ticker}/{timeframe.Multiplier}/{timeframe.Timespan}/{DateKey(date)}", stocksResponse);
    }

    public TickerDetails GetTickerDetails(string ticker)
    {
        return memoryCache.Get<TickerDetails>($"TickerDetails/{ticker}");
    }

    public void SetTickerDetails(TickerDetails tickerDetails)
    {
        memoryCache.Set($"TickerDetails/{tickerDetails.Ticker}", tickerDetails);
    }


    /// <summary>
    /// Completed websocket bars kept per ticker after minute rollover. Sized to cover
    /// several missed snapshot polls: used for the scan blind window between minute
    /// close and the snapshot landing, the snapshot-vs-websocket rollover diff, and
    /// tier-1 gap backfill.
    /// </summary>
    private const int RecentLiveBarCount = 5;

    public void AddLiveBar(MassiveWebsocketAggregateResponse webSocketBar)
    {
        var currentBar = memoryCache.Get<Bar>(webSocketBar.Ticker);

        if (currentBar is null || webSocketBar.TickStart / 60_000 > currentBar.Timestamp / 60_000)
        {
            if (currentBar is not null)
            {
                PushRecentLiveBar(webSocketBar.Ticker, currentBar);
            }

            var bar = new Bar
            {
                Close = webSocketBar.Close,
                High = webSocketBar.High,
                Low = webSocketBar.Low,
                Open = webSocketBar.Open,
                Volume = webSocketBar.Volume,
                Vwap = webSocketBar.TickVwap,
                Timestamp = webSocketBar.TickStart,
            };

            if (webSocketBar.Ticker is "SPY")
            {
                var bars = memoryCache.Get<List<Bar>>("SPY_LIVE");

                if (bars is null)
                {
                    bars = [bar];
                    memoryCache.Set("SPY_LIVE", bars);
                }
                else
                {
                    bars.Add(bar);
                    memoryCache.Set("SPY_LIVE", bars);
                }
            }

            memoryCache.Set(webSocketBar.Ticker, bar);
            return;
        }

        if (webSocketBar.High > currentBar.High)
        {
            currentBar.High = webSocketBar.High;
        }

        if (webSocketBar.Low < currentBar.Low)
        {
            currentBar.Low = webSocketBar.Low;
        }

        currentBar.Close = webSocketBar.Close;
        currentBar.Vwap = webSocketBar.TickVwap;
        currentBar.Volume += webSocketBar.Volume;
    }

    public Bar GetLiveBar(string ticker)
    {
        var bar = memoryCache.Get<Bar>(ticker);

        return bar;
    }

    public IReadOnlyList<Bar> GetRecentLiveBars(string ticker)
    {
        return memoryCache.Get<IReadOnlyList<Bar>>($"LiveBars/{ticker}") ?? [];
    }

    /// <summary>
    /// Copy-on-write: the writer is the single websocket receive loop, but scans and
    /// the snapshot job read concurrently, so the stored list is never mutated —
    /// each push swaps in a new one. The bar itself is safe to share because it is
    /// only mutated while it is the in-progress bar, before rollover pushes it here.
    /// </summary>
    private void PushRecentLiveBar(string ticker, Bar completedBar)
    {
        var existing = memoryCache.Get<IReadOnlyList<Bar>>($"LiveBars/{ticker}");

        var updated = existing is null ? new List<Bar>(RecentLiveBarCount + 1) : new List<Bar>(existing);
        updated.Add(completedBar);

        if (updated.Count > RecentLiveBarCount)
        {
            updated.RemoveAt(0);
        }

        memoryCache.Set<IReadOnlyList<Bar>>($"LiveBars/{ticker}", updated, ExpireIn);
    }
}
