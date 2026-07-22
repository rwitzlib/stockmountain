using Amazon.S3;
using Amazon.S3.Model;
using MarketViewer.Api.HostedServices;
using MarketViewer.Application.Handlers.Data.Tickers;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.MarketData;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Models.Snapshot;
using MarketViewer.Contracts.Responses.Market;
using MarketViewer.Contracts.Responses.Tools;
using Microsoft.Extensions.Caching.Memory;
using Massive.Client.Interfaces;
using Massive.Client.Models;
using Massive.Client.Requests;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Snapshot = MarketViewer.Contracts.Models.Snapshot.Snapshot;

namespace MarketViewer.Api.Services;

/// <summary>
/// Populates the live market cache. Historical data is bulk-loaded from the S3
/// aggregate files produced nightly by the market-data aggregator Lambda (a handful
/// of GETs instead of thousands of per-ticker API calls); only the current session,
/// missing from S3 on a mid-day restart, is topped up per ticker from Massive.
/// Runs at startup, daily at 3:30am ET, and as recovery after failures.
/// </summary>
public class MarketCacheWarmer(
    TickerCache tickerCache,
    TickerHandler tickerHandler,
    IAmazonS3 s3Client,
    IMarketCache marketCache,
    IMemoryCache memoryCache,
    IMassiveClient massiveClient,
    CacheWarmupState warmupState,
    BarCacheService barCacheService,
    ILogger<MarketCacheWarmer> logger)
{
    private const int MinuteFileCount = 5;
    private const int MinuteFileLookbackDays = 14;

    private static readonly TimeSpan[] AttemptDelays = [TimeSpan.Zero, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly TimeZoneInfo _timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    private readonly string _bucketName = Environment.GetEnvironmentVariable("MARKET_DATA_BUCKET_NAME") ?? MarketDataStorageContract.DefaultBucketName;

    public async Task RunWarmupAsync(CancellationToken cancellationToken)
    {
        warmupState.MarkWarming();

        for (var attempt = 0; attempt < AttemptDelays.Length; attempt++)
        {
            if (AttemptDelays[attempt] > TimeSpan.Zero)
            {
                await Task.Delay(AttemptDelays[attempt], cancellationToken);
            }

            try
            {
                await WarmupOnce(cancellationToken);
                warmupState.MarkReady();

                // Snapshots that raced in between the merge inside WarmupOnce and
                // MarkReady would otherwise sit in the buffer until the next warmup.
                MergeBufferedSnapshots();
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Cache warmup attempt {attempt}/{total} failed: {message}", attempt + 1, AttemptDelays.Length, ex.Message);

                if (attempt == AttemptDelays.Length - 1)
                {
                    warmupState.MarkFailed(ex.Message);
                }
            }
        }
    }

    #region Private Methods

    private async Task WarmupOnce(CancellationToken cancellationToken)
    {
        var sp = Stopwatch.StartNew();
        var nowEt = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, _timeZone);

        logger.LogInformation("Starting cache warmup at {time} ET.", nowEt);

        await PopulateTickerIdMap();
        var tickers = await PopulateTickerDetails();

        var minute = await LoadMinuteAggregates(nowEt, cancellationToken);
        var hour = await LoadMonthlyOrYearlyAggregates(Timespan.hour, [nowEt.AddMonths(-1), nowEt], cancellationToken);
        var day = await LoadMonthlyOrYearlyAggregates(Timespan.day, [nowEt.AddYears(-1), nowEt], cancellationToken);

        if (minute.IsEmpty)
        {
            throw new InvalidOperationException($"No minute aggregate files found in s3://{_bucketName} for the last {MinuteFileLookbackDays} days.");
        }

        if (IsDuringOrAfterSession(nowEt))
        {
            await TopUpToday(tickers, minute, hour, day, nowEt, cancellationToken);
        }

        StoreAggregates(minute, new Timeframe(1, Timespan.minute));
        StoreAggregates(hour, new Timeframe(1, Timespan.hour));
        StoreAggregates(day, new Timeframe(1, Timespan.day));

        SetSnapshot();
        MergeBufferedSnapshots();

        sp.Stop();
        logger.LogInformation(
            "Finished cache warmup. Tickers: {tickers}, minute: {minute}, hour: {hour}, day: {day}. Time elapsed: {elapsed}ms.",
            tickers.Count, minute.Count, hour.Count, day.Count, sp.ElapsedMilliseconds);
    }

    private async Task PopulateTickerIdMap()
    {
        try
        {
            var response = await tickerHandler.GetSnapshot();

            if (response.Status == HttpStatusCode.OK && response.Data != null)
            {
                var snapshotData = response.Data;

                tickerCache.IdToSymbol = new string[snapshotData.MaxId + 1];

                for (int i = 1; i <= snapshotData.MaxId; i++)
                {
                    var symbol = snapshotData.Symbols[i];

                    if (!string.IsNullOrWhiteSpace(symbol))
                    {
                        tickerCache.IdToSymbol[i] = symbol;
                        tickerCache.SymbolToId[symbol] = i;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error populating ticker id map: {message}", ex.Message);
        }
    }

    private async Task<List<string>> PopulateTickerDetails()
    {
        var request = new GetObjectRequest
        {
            BucketName = _bucketName,
            Key = MarketDataStorageContract.TickerDetailsKey
        };
        using var s3Response = await s3Client.GetObjectAsync(request);
        var tickerDetailsList = await JsonSerializer.DeserializeAsync<List<TickerDetails>>(s3Response.ResponseStream) ?? [];

        foreach (var tickerDetails in tickerDetailsList)
        {
            marketCache.SetTickerDetails(tickerDetails);
        }

        var tickers = tickerDetailsList.Select(tickerDetails => tickerDetails.Ticker).ToList();
        marketCache.SetTickers(tickers);

        return tickers;
    }

    /// <summary>
    /// Loads the most recent daily minute-aggregate files (newest first, skipping
    /// weekends and dates whose file doesn't exist yet), then merges them oldest-first.
    /// </summary>
    private async Task<ConcurrentDictionary<string, StocksResponse>> LoadMinuteAggregates(DateTimeOffset nowEt, CancellationToken cancellationToken)
    {
        var files = new List<(DateTimeOffset Date, List<StocksResponse> Responses)>();

        for (var daysBack = 0; daysBack <= MinuteFileLookbackDays && files.Count < MinuteFileCount; daysBack++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var date = nowEt.AddDays(-daysBack);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            var responses = await TryLoadAggregateFile(date, Timespan.minute);
            if (responses is not null)
            {
                files.Add((date, responses));
            }
        }

        var map = new ConcurrentDictionary<string, StocksResponse>();
        foreach (var (_, responses) in Enumerable.Reverse(files))
        {
            MergeInto(map, responses);
        }

        return map;
    }

    /// <summary>
    /// Loads period-scoped aggregate files (hour files cover a month, day files a
    /// year) for the given dates, oldest first. Missing files are skipped — e.g.
    /// there is no previous-month file when the bucket is young.
    /// </summary>
    private async Task<ConcurrentDictionary<string, StocksResponse>> LoadMonthlyOrYearlyAggregates(Timespan timespan, DateTimeOffset[] dates, CancellationToken cancellationToken)
    {
        var map = new ConcurrentDictionary<string, StocksResponse>();

        foreach (var date in dates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var responses = await TryLoadAggregateFile(date, timespan);
            if (responses is not null)
            {
                MergeInto(map, responses);
            }
        }

        return map;
    }

    private async Task<List<StocksResponse>> TryLoadAggregateFile(DateTimeOffset date, Timespan timespan)
    {
        var key = MarketDataStorageContract.BuildAggregateKey(date, 1, timespan);

        try
        {
            var sp = Stopwatch.StartNew();
            using var s3Response = await s3Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            });

            // Deserialize straight from the S3 stream; materializing the file as a
            // string first doubles it (UTF-16) on the Large Object Heap.
            var responses = await JsonSerializer.DeserializeAsync<List<StocksResponse>>(s3Response.ResponseStream, SerializerOptions);

            sp.Stop();
            logger.LogInformation("Loaded aggregate file {key}: {count} tickers in {elapsed}ms.", key, responses?.Count ?? 0, sp.ElapsedMilliseconds);

            return responses;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogInformation("Aggregate file {key} does not exist; skipping.", key);
            return null;
        }
    }

    private static void MergeInto(ConcurrentDictionary<string, StocksResponse> map, IEnumerable<StocksResponse> responses)
    {
        foreach (var response in responses)
        {
            if (response?.Ticker is null || response.Results is not { Count: > 0 })
            {
                continue;
            }

            if (!map.TryGetValue(response.Ticker, out var existing))
            {
                map[response.Ticker] = new StocksResponse
                {
                    Ticker = response.Ticker,
                    Results = [.. response.Results]
                };
                continue;
            }

            var lastTimestamp = existing.Results[^1].Timestamp;
            existing.Results.AddRange(response.Results.Where(bar => bar.Timestamp > lastTimestamp));
        }
    }

    /// <summary>
    /// S3 files only cover completed sessions, so on a restart during or after a
    /// trading session today's bars are fetched per ticker from Massive (minute only;
    /// hour and day bars are derived from the minute bars).
    /// </summary>
    private async Task TopUpToday(
        List<string> tickers,
        ConcurrentDictionary<string, StocksResponse> minute,
        ConcurrentDictionary<string, StocksResponse> hour,
        ConcurrentDictionary<string, StocksResponse> day,
        DateTimeOffset nowEt,
        CancellationToken cancellationToken)
    {
        var sp = Stopwatch.StartNew();
        var today = nowEt.ToString("yyyy-MM-dd");
        var dayTimestamp = new DateTimeOffset(nowEt.Date, _timeZone.GetUtcOffset(nowEt)).ToUnixTimeMilliseconds();

        logger.LogInformation("Topping up today's ({today}) bars for {count} tickers from Massive.", today, tickers.Count);

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(tickers, options, async (ticker, ct) =>
        {
            try
            {
                var response = await massiveClient.GetAggregates(new MassiveAggregateRequest
                {
                    Ticker = ticker,
                    Multiplier = 1,
                    Timespan = Timespan.minute.ToString(),
                    From = today,
                    To = today,
                    Limit = 50000
                });

                var fetchedBars = response?.Results?.ToList();
                if (fetchedBars is not { Count: > 0 })
                {
                    return;
                }

                var todayBars = AppendNewBars(minute, ticker, fetchedBars);
                if (todayBars.Count == 0)
                {
                    return;
                }

                var hourBars = todayBars
                    .GroupBy(bar => bar.Timestamp / 3_600_000)
                    .Select(group => AggregateBars([.. group], group.Key * 3_600_000))
                    .ToList();
                AppendNewBars(hour, ticker, hourBars);

                AppendNewBars(day, ticker, [AggregateBars(todayBars, dayTimestamp)]);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error topping up today's bars for {ticker}: {message}", ticker, ex.Message);
            }
        });

        sp.Stop();
        logger.LogInformation("Finished topping up today's bars. Time elapsed: {elapsed}ms.", sp.ElapsedMilliseconds);
    }

    /// <summary>
    /// Appends bars newer than the ticker's last cached bar; creates the entry if the
    /// ticker has no historical data. Returns the bars that were actually appended.
    /// </summary>
    private static List<Bar> AppendNewBars(ConcurrentDictionary<string, StocksResponse> map, string ticker, List<Bar> bars)
    {
        var entry = map.GetOrAdd(ticker, t => new StocksResponse
        {
            Ticker = t,
            Results = []
        });

        // Warmup owns these response objects until they are stored in the cache, and
        // each ticker is only touched by one worker, so no further locking is needed.
        var lastTimestamp = entry.Results.Count > 0 ? entry.Results[^1].Timestamp : long.MinValue;
        var newBars = bars.Where(bar => bar.Timestamp > lastTimestamp).ToList();
        entry.Results.AddRange(newBars);

        return newBars;
    }

    private static Bar AggregateBars(List<Bar> bars, long timestamp)
    {
        var close = bars[^1].Close;
        var high = bars.Max(bar => bar.High);
        var low = bars.Min(bar => bar.Low);

        return new Bar
        {
            Timestamp = timestamp,
            Open = bars[0].Open,
            Close = close,
            High = high,
            Low = low,
            Volume = bars.Sum(bar => bar.Volume),
            TransactionCount = bars.Sum(bar => bar.TransactionCount),
            Vwap = (close + high + low) / 3
        };
    }

    private void StoreAggregates(ConcurrentDictionary<string, StocksResponse> map, Timeframe timeframe)
    {
        var now = DateTimeOffset.Now;

        foreach (var response in map.Values)
        {
            marketCache.SetStocksResponse(response, timeframe, now);
        }

        marketCache.SetTickersByTimeframe(now, timeframe, map.Keys.ToList());
    }

    private static bool IsDuringOrAfterSession(DateTimeOffset nowEt)
    {
        return nowEt.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && nowEt.Hour >= 4;
    }

    private void SetSnapshot()
    {
        try
        {
            var now = DateTimeOffset.Now;
            var snapshotResponse = new SnapshotResponse
            {
                Entries = []
            };

            var minuteTime = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, 0, now.Offset).AddMinutes(-1);
            var hourTime = new DateTimeOffset(minuteTime.Year, minuteTime.Month, minuteTime.Day, minuteTime.Hour, 0, 0, minuteTime.Offset);

            var tickers = marketCache.GetTickers();
            foreach (var ticker in tickers)
            {
                // No response-level Clone here: it deep-copies every cached bar for
                // every ticker only to extract two bars below. Cloning just those
                // two keeps the snapshot isolated from the live cache.
                var minute = marketCache.GetStocksResponse(ticker, new Timeframe(1, Timespan.minute), DateTimeOffset.Now);
                var hour = marketCache.GetStocksResponse(ticker, new Timeframe(1, Timespan.hour), DateTimeOffset.Now);

                if (minute is null || hour is null)
                {
                    continue;
                }

                snapshotResponse.Entries.Add(new SnapshotEntry
                {
                    Ticker = ticker,
                    Results =
                    [
                        new Snapshot
                        {
                            Timestamp = minuteTime.ToUnixTimeMilliseconds(),
                            DateTime = minuteTime,
                            Minute = minute.Results?.FirstOrDefault(q => q.Timestamp == minuteTime.ToUnixTimeMilliseconds())?.Clone(),
                            Hour = hour.Results?.FirstOrDefault(q => q.Timestamp == hourTime.ToUnixTimeMilliseconds())?.Clone()
                        }
                    ]
                });
            }

            memoryCache.Set("snapshot", snapshotResponse);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting snapshot data: {message}", ex.Message);
        }
    }

    private void MergeBufferedSnapshots()
    {
        try
        {
            var bufferedSnapshots = warmupState.DrainBuffer();
            var orderedSnapshots = bufferedSnapshots
                .Where(s => s?.Tickers?.Any() == true)
                .OrderBy(s => s.Tickers.First().Minute?.Timestamp ?? 0)
                .ToList();

            if (!orderedSnapshots.Any())
            {
                return;
            }

            foreach (var snapshot in orderedSnapshots)
            {
                foreach (var ticker in snapshot.Tickers)
                {
                    if (ticker?.Minute == null)
                    {
                        continue;
                    }
                    barCacheService.AddBarToCache(ticker.Ticker, new Timeframe(1, Timespan.minute), ticker.Minute);
                    barCacheService.AddBarToCache(ticker.Ticker, new Timeframe(1, Timespan.hour), ticker.Minute);
                    barCacheService.AddBarToCache(ticker.Ticker, new Timeframe(1, Timespan.day), ticker.Minute);
                }
            }

            logger.LogInformation("Merged {count} buffered snapshots into cache.", orderedSnapshots.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error merging buffered snapshots: {message}", ex.Message);
        }
    }

    #endregion
}
