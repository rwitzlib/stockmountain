using MarketViewer.Api.HostedServices;
using MarketViewer.Api.Services;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Enums;
using Quartz;
using System.Diagnostics;
using Massive.Client.Interfaces;
using Massive.Client.Models;
using Massive.Client.Requests;
using Microsoft.Extensions.Caching.Memory;
using Massive.Client.Responses;
using Snapshot = MarketViewer.Contracts.Models.Snapshot.Snapshot;
using MarketViewer.Contracts.Responses.Tools;
using MarketViewer.Contracts.Models;

namespace MarketViewer.Api.Jobs;

public class SnapshotJob(
    IMemoryCache memoryCache,
    IMassiveClient massiveClient,
    CacheWarmupState warmupState,
    BarCacheService barCacheService,
    ILogger<SnapshotJob> logger) : IJob
{
    private readonly Stopwatch _sp = new();

    private readonly TimeZoneInfo _timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    public async Task Execute(IJobExecutionContext context)
    {
        _sp.Start();

        try
        {
            logger.LogInformation("Started snapshot job at: {time}.", DateTimeOffset.Now);

            var massiveSnapshotResponse = await massiveClient.GetAllTickersSnapshot(null);

            // During warmup, buffer snapshots instead of applying them directly
            // This prevents data loss while aggregates are being populated
            if (!warmupState.IsWarmupComplete)
            {
                warmupState.BufferSnapshot(massiveSnapshotResponse);
                _sp.Stop();
                logger.LogInformation("Buffered snapshot during warmup. Total buffered: {count}. Time elapsed: {elapsed}ms.", 
                    warmupState.BufferedCount, _sp.ElapsedMilliseconds);
                return;
            }

            var spy = massiveSnapshotResponse.Tickers.FirstOrDefault(q => q.Ticker == "SPY");
            if (spy is not null)
            {
                logger.LogInformation("Current snapshot: {unix} - {datetime}", spy.Minute.Timestamp, DateTimeOffset.FromUnixTimeMilliseconds(spy.Minute.Timestamp).ToOffset(_timeZone.GetUtcOffset(DateTimeOffset.Now)));
            }

            var snapshotResponse = memoryCache.Get<SnapshotResponse>("snapshot");

            foreach (var snapshot in massiveSnapshotResponse.Tickers)
            {
                var minuteCandle = barCacheService.AddBarToCache(snapshot.Ticker, new Timeframe(1, Timespan.minute), snapshot.Minute);
                var hourCandle = barCacheService.AddBarToCache(snapshot.Ticker, new Timeframe(1, Timespan.hour), snapshot.Minute);
                var dayCandle = barCacheService.AddBarToCache(snapshot.Ticker, new Timeframe(1, Timespan.day), snapshot.Minute);
                //var snapshotEntry = snapshotResponse.Entries.FirstOrDefault(q => q.Ticker == snapshot.Ticker);

                //if (snapshotEntry is null || snapshotEntry.Results is null)
                //{
                //    continue;
                //}

                //var offset = _timeZone.GetUtcOffset(DateTimeOffset.FromUnixTimeMilliseconds(snapshot.Minute.Timestamp));

                //snapshotEntry.Results.Add(new Snapshot
                //{
                //    Timestamp = snapshot.Minute.Timestamp,
                //    DateTime = DateTimeOffset.FromUnixTimeMilliseconds(snapshot.Minute.Timestamp).ToOffset(offset),
                //    Minute = minuteCandle?.Clone(),
                //    Hour = hourCandle?.Clone()
                //});
            }

            memoryCache.Set("snapshot", snapshotResponse);

            _sp.Stop();
            logger.LogInformation("Finished snapshot job at: {time}. Time elapsed: {elapsed}ms.", DateTimeOffset.Now, _sp.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError("Error during snapshot job: {message}", ex.Message);
        }
    }
}
