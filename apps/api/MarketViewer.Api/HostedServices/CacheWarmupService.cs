using Amazon.S3;
using Amazon.S3.Model;
using MarketViewer.Api.Jobs;
using MarketViewer.Api.Services;
using MarketViewer.Application.Handlers.Data.Tickers;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.MarketData;
using MarketViewer.Contracts.Models;
using MarketViewer.Contracts.Models.Snapshot;
using MarketViewer.Contracts.Responses.Tools;
using MarketViewer.Infrastructure.Mapping;
using Microsoft.Extensions.Caching.Memory;
using Polygon.Client.Interfaces;
using Polygon.Client.Models;
using Polygon.Client.Requests;
using Quartz;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Snapshot = MarketViewer.Contracts.Models.Snapshot.Snapshot;

namespace MarketViewer.Api.HostedServices
{
    public class CacheWarmupService(
        TickerCache cache,
        TickerHandler tickerHandler,
        IAmazonS3 s3Client,
        IMarketCache marketCache,
        ISchedulerFactory schedulerFactory,
        IMemoryCache memoryCache,
        IPolygonClient polygonClient,
        CacheWarmupState warmupState,
        BarCacheService barCacheService,
        ILogger<CacheWarmupService> logger) : IHostedLifecycleService
    {
        public Task StartingAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
        
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            return;
            await PopulateTickers();
            await PopulateTickerDetailsAndMarketCache();
        }

        public async Task StartedAsync(CancellationToken cancellationToken)
        {
            return;
            var now = DateTimeOffset.Now;

            if (now.Second >= 5)
            {
                // Wait until the start of the next minute
                await Task.Delay(now.AddMinutes(1).AddSeconds(-now.Second).AddMilliseconds(-now.Millisecond) - now, cancellationToken);
            }

            // 1. Start snapshot collection on a dedicated thread (not thread pool)
            // This prevents thread pool starvation from blocking snapshot collection
            var snapshotCollectionTask = Task.Factory.StartNew(
                () => CollectSnapshotsDuringWarmup(cancellationToken),
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default
            ).Unwrap();

            // 2. Populate aggregates (this is the slow part that may take several minutes)
            await PopulateAggregates();

            // 3. Mark warmup as complete - this will stop the snapshot collection loop
            logger.LogInformation("Aggregate population complete. Waiting for snapshot collection to finish...");
            warmupState.MarkWarmupComplete();
            
            // Wait for the snapshot collection task to finish its current iteration
            await snapshotCollectionTask;

            // 4. Merge any buffered snapshots
            logger.LogInformation("Merging {count} buffered snapshots.", warmupState.BufferedCount);
            MergeBufferedSnapshots();

            // 5. Schedule all jobs for normal operation
            var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
            await ScheduleSnapshotJob(scheduler, cancellationToken);

            var populateScannerJob = JobBuilder.Create<PopulateScannerJob>()
                .Build();
            var populateScannerTrigger = TriggerBuilder.Create()
                .StartAt(DateTimeOffset.Now)
                .ForJob(populateScannerJob)
                .WithSimpleSchedule(x => x
                    .WithIntervalInMinutes(5)
                    .RepeatForever())
                .Build();
            await scheduler.ScheduleJob(populateScannerJob, populateScannerTrigger, cancellationToken);

            var scannerJob = JobBuilder.Create<ScannerJob>()
                .Build();
            var scannerTrigger = TriggerBuilder.Create()
                .StartAt(DateTimeOffset.Now.AddSeconds(15))
                .ForJob(scannerJob)
                .WithSimpleSchedule(x => x
                    .WithIntervalInSeconds(15)
                    .RepeatForever())
                .Build();
            await scheduler.ScheduleJob(scannerJob, scannerTrigger, cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StoppedAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StoppingAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        #region Private Methods

        private async Task PopulateTickers()
        {
            try
            {
                var response = await tickerHandler.GetSnapshot();

                if (response.Status == HttpStatusCode.OK && response.Data != null)
                {
                    var snapshotData = response.Data;

                    cache.IdToSymbol = new string[snapshotData.MaxId + 1];

                    for (int i = 1; i <= snapshotData.MaxId; i++)
                    {
                        var symbol = snapshotData.Symbols[i];

                        if (!string.IsNullOrWhiteSpace(symbol))
                        {
                            cache.IdToSymbol[i] = symbol;
                            cache.SymbolToId[symbol] = i;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during cache warmup: {Message}", ex.Message);
            }
        }

        private async Task PopulateTickerDetailsAndMarketCache()
        {
            try
            {
                var sp = Stopwatch.StartNew();
                var date = DateTimeOffset.Now;

                logger.LogInformation("Started populating ticker data at: {time}.", date);

                var request = new GetObjectRequest
                {
                    BucketName = Environment.GetEnvironmentVariable("MARKET_DATA_BUCKET_NAME") ?? MarketDataStorageContract.DefaultBucketName,
                    Key = MarketDataStorageContract.TickerDetailsKey
                };
                var s3Response = await s3Client.GetObjectAsync(request);

                using var streamReader = new StreamReader(s3Response.ResponseStream);
                var json = await streamReader.ReadToEndAsync();

                var tickerDetailsList = JsonSerializer.Deserialize<IEnumerable<TickerDetails>>(json) ?? Enumerable.Empty<TickerDetails>();

                foreach (var tickerDetails in tickerDetailsList)
                {
                    marketCache.SetTickerDetails(tickerDetails);
                }

                var tickers = tickerDetailsList.Select(tickerDetails => tickerDetails.Ticker);

                marketCache.SetTickers(tickers);
                marketCache.SetTickersByTimeframe(date, new Timeframe(1, Timespan.minute), tickers);
                marketCache.SetTickersByTimeframe(date, new Timeframe(1, Timespan.hour), tickers);
                marketCache.SetTickersByTimeframe(date, new Timeframe(1, Timespan.day), tickers);

                sp.Stop();

                logger.LogInformation("Finished populating ticker data at: {time}. Time elapsed: {elapsed}ms.", date, sp.ElapsedMilliseconds);

            }
            catch (Exception ex)
            {
                logger.LogError("Error populating ticker data: {message}", ex.Message);
            }
        }

        private async Task PopulateAggregates()
        {
            try
            {
                var sp = new Stopwatch();
                sp.Start();

                logger.LogInformation("Initializing aggregate data at: {time}.", DateTimeOffset.Now);

                var timeframes = new List<Timeframe>
                {
                    new (1, Timespan.minute),
                    new (1, Timespan.hour),
                    new (1, Timespan.day)
                };

                foreach (var timeframe in timeframes)
                {
                    await PopulateStocksResponses(timeframe, DateTimeOffset.Now);
                    logger.LogInformation("Finished initializing {timespan} aggregate data at: {time}. Time elapsed: {elapsed}ms.", timeframe.Timespan, DateTimeOffset.Now, sp.ElapsedMilliseconds);
                }

                SetSnapshot();

                sp.Stop();
                logger.LogInformation("Finished initializing all aggregate data. Total time elapsed: {elapsed}ms.", sp.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                logger.LogError("Error initializing aggregate data: {message}", ex.Message);
            }
        }

        private async Task PopulateStocksResponses(Timeframe timeframe, DateTimeOffset date)
        {
            try
            {
                var tickers = marketCache.GetTickers();

                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                };

                await Parallel.ForEachAsync(tickers, options, async (ticker, cancellationToken) => 
                {
                    try
                    {
                        var start = date.Add(GetStartOffset(timeframe.Timespan));

                        var polygonAggregateRequest = new PolygonAggregateRequest
                        {
                            Ticker = ticker,
                            Multiplier = timeframe.Multiplier,
                            Timespan = timeframe.Timespan.ToString(),
                            From = start.ToString("yyyy-MM-dd"),
                            To = DateTime.Now.ToString("yyyy-MM-dd"),
                            Limit = 50000
                        };

                        var polygonAggregateResponse = await polygonClient.GetAggregates(polygonAggregateRequest);
                        var stocksResponse = AggregateMapper.ToStocksResponse(polygonAggregateResponse);

                        marketCache.SetStocksResponse(stocksResponse, timeframe, date);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("Error populating stocks response for {ticker} {timespan}: {message}", ticker, timeframe.Timespan, ex.Message);
                    }
                });
            }
            catch (Exception ex)
            {
                logger.LogError("Error populating stocks responses for {timespan}: {message}", timeframe.Timespan, ex.Message);
            }
        }

        private static TimeSpan GetStartOffset(Timespan timespan)
        {
            return timespan switch
            {
                Timespan.minute => TimeSpan.FromDays(-5),
                Timespan.hour => TimeSpan.FromDays(-30),
                Timespan.day => TimeSpan.FromDays(-365),
                Timespan.week => throw new NotImplementedException(),
                Timespan.month => throw new NotImplementedException(),
                Timespan.quarter => throw new NotImplementedException(),
                Timespan.year => throw new NotImplementedException(),
                _ => throw new NotImplementedException()
            };
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
                    var minute = marketCache.GetStocksResponse(ticker, new Timeframe(1, Timespan.minute), DateTimeOffset.Now)?.Clone();
                    var hour = marketCache.GetStocksResponse(ticker, new Timeframe(1, Timespan.hour), DateTimeOffset.Now)?.Clone();

                    if (minute is null || hour is null)
                    {
                        continue;
                    }

                    var entry = snapshotResponse.Entries.FirstOrDefault(q => q.Ticker == ticker);

                    if (entry is null || entry.Results is null)
                    {
                        snapshotResponse.Entries.Add(new SnapshotEntry
                        {
                            Ticker = ticker,
                            Results = new List<Snapshot>()
                        });
                        entry = snapshotResponse.Entries.FirstOrDefault(q => q.Ticker == ticker);
                    }


                    entry.Results.Add(new Snapshot
                    {
                        Timestamp = minuteTime.ToUnixTimeMilliseconds(),
                        DateTime = minuteTime,
                        Minute = minute.Results?.FirstOrDefault(q => q.Timestamp == minuteTime.ToUnixTimeMilliseconds())?.Clone(),
                        Hour = hour.Results?.FirstOrDefault(q => q.Timestamp == hourTime.ToUnixTimeMilliseconds())?.Clone()
                    });
                }

                memoryCache.Set("snapshot", snapshotResponse);
            }
            catch (Exception ex)
            {
                logger.LogError("Error setting snapshot data: {message}", ex.Message);
            }
        }

        private async Task CollectSnapshotsDuringWarmup(CancellationToken cancellationToken)
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
            
            logger.LogInformation("Starting snapshot collection during warmup on dedicated thread.");
            
            while (!warmupState.IsWarmupComplete && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Wait until 3 seconds into the next minute
                    var now = DateTimeOffset.Now;
                    var nextMinute = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, 0, now.Offset)
                        .AddMinutes(1)
                        .AddSeconds(3);
                    
                    var delay = nextMinute - DateTimeOffset.Now;
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, cancellationToken);
                    }

                    // Check again after delay in case warmup completed while waiting
                    if (warmupState.IsWarmupComplete || cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    var sp = Stopwatch.StartNew();
                    var polygonSnapshotResponse = await polygonClient.GetAllTickersSnapshot(null);
                    
                    warmupState.BufferSnapshot(polygonSnapshotResponse);
                    
                    sp.Stop();
                    
                    var spy = polygonSnapshotResponse.Tickers?.FirstOrDefault(q => q.Ticker == "SPY");
                    if (spy?.Minute != null)
                    {
                        logger.LogInformation(
                            "Buffered snapshot during warmup. Snapshot time: {snapshotTime}, Total buffered: {count}, Fetch time: {elapsed}ms",
                            DateTimeOffset.FromUnixTimeMilliseconds(spy.Minute.Timestamp).ToOffset(timeZone.GetUtcOffset(DateTimeOffset.Now)),
                            warmupState.BufferedCount,
                            sp.ElapsedMilliseconds);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError("Error collecting snapshot during warmup: {message}", ex.Message);
                }
            }
            
            logger.LogInformation("Snapshot collection during warmup completed. Total buffered: {count}", warmupState.BufferedCount);
        }

        private async Task ScheduleSnapshotJob(IScheduler scheduler, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.Now;
            // Start 3 seconds into the next minute to ensure data is available
            var startTime = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.AddMinutes(1).Minute, 3, 0, now.Offset);

            var scheduledSnapshotJob = JobBuilder.Create<SnapshotJob>()
                .StoreDurably(true)
                .Build();

            var scheduledSnapshotTrigger = TriggerBuilder.Create()
                .WithSimpleSchedule(schedule => schedule
                    .WithIntervalInMinutes(1)
                    .WithRepeatCount(540)) // 9 hours
                .ForJob(scheduledSnapshotJob)
                .StartAt(startTime)
                .Build();

            await scheduler.ScheduleJob(scheduledSnapshotJob, scheduledSnapshotTrigger, cancellationToken);

            logger.LogInformation("Snapshot job scheduled to start at {startTime} for normal operation.", startTime);
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
                    logger.LogInformation("No buffered snapshots to merge.");
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

                logger.LogInformation("Successfully merged {count} buffered snapshots into cache.", orderedSnapshots.Count);
            }
            catch (Exception ex)
            {
                logger.LogError("Error merging buffered snapshots: {message}", ex.Message);
            }
        }

        #endregion
    }
}
