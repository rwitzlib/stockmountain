using MarketViewer.Api.Config;
using MarketViewer.Api.HostedServices;
using MarketViewer.Api.Services;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Models;
using Massive.Client.Interfaces;
using Massive.Client.Requests;
using Microsoft.Extensions.Caching.Memory;
using Quartz;
using System.Diagnostics;
using Bar = Massive.Client.Models.Bar;

namespace MarketViewer.Api.Jobs;

/// <summary>
/// Once per minute: waits for the provider to flush the latest completed minute bar
/// (freshness is judged by advancement of SPY's bar, never by wall clock — the data
/// plan may be delayed), applies the all-tickers snapshot to the cache, diffs the
/// snapshot bars against the websocket-built bars from the live feed, and backfills
/// any minute gaps a missed poll left behind. Emits one wide event per run.
/// </summary>
[DisallowConcurrentExecution]
public class SnapshotJob(
    SnapshotConfig config,
    IMemoryCache memoryCache,
    IMassiveClient massiveClient,
    IMarketCache marketCache,
    CacheWarmupState warmupState,
    BarCacheService barCacheService,
    ILogger<SnapshotJob> logger) : IJob
{
    private const string LastSpyMinuteKey = "SnapshotJob/LastSpyMinuteTs";

    private const float PriceTolerance = 0.005f;
    private const float VolumeRelativeTolerance = 0.005f;

    private static readonly TimeZoneInfo TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    private sealed record GapRecord(string Ticker, long FromTs, long ToTs);

    private sealed record MismatchRecord(string Ticker, float RelVolumeDelta, Bar WsBar, Bar SnapshotBar);

    public async Task Execute(IJobExecutionContext context)
    {
        var sp = Stopwatch.StartNew();

        try
        {
            // During warmup, buffer snapshots instead of applying them directly.
            // This prevents data loss while aggregates are being populated. No probe:
            // buffered snapshots are deduped by timestamp when merged.
            if (!warmupState.IsReady)
            {
                var bufferedResponse = await massiveClient.GetAllTickersSnapshot(null);
                warmupState.BufferSnapshot(bufferedResponse);
                logger.LogInformation("Buffered snapshot during warmup. Total buffered: {count}. Time elapsed: {elapsed}ms.",
                    warmupState.BufferedCount, sp.ElapsedMilliseconds);
                return;
            }

            var probe = await ProbeForFreshBar(context.CancellationToken);

            var snapshotResponse = await massiveClient.GetAllTickersSnapshot(null);
            var snapshots = snapshotResponse?.Tickers?.ToList();

            if (snapshots is not { Count: > 0 })
            {
                logger.LogWarning("Snapshot returned no tickers; skipping run.");
                return;
            }

            var spyTimestamp = snapshots
                .FirstOrDefault(q => q.Ticker == "SPY")?.Minute?.Timestamp;

            var appended = 0;
            var wsMatched = 0;
            var wsPriceMismatch = 0;
            var wsVolumeMismatch = 0;
            var wsMissing = 0;
            List<GapRecord> gaps = [];
            List<MismatchRecord> mismatches = [];

            foreach (var snapshot in snapshots)
            {
                if (snapshot?.Minute is null)
                {
                    continue;
                }

                var minuteResult = barCacheService.AddBarToCache(snapshot.Ticker, new Timeframe(1, Timespan.minute), snapshot.Minute);
                barCacheService.AddBarToCache(snapshot.Ticker, new Timeframe(1, Timespan.hour), snapshot.Minute);
                barCacheService.AddBarToCache(snapshot.Ticker, new Timeframe(1, Timespan.day), snapshot.Minute);

                if (minuteResult.Added is null)
                {
                    continue;
                }

                appended++;

                if (minuteResult.HasGap)
                {
                    gaps.Add(new GapRecord(snapshot.Ticker, minuteResult.GapFromTimestamp!.Value, minuteResult.GapToTimestamp!.Value));
                }

                // Rollover diff: same minute, websocket-built vs snapshot. The match
                // rate answers whether websocket bars could stand alone; a wsMissing
                // spike doubles as a live-feed liveness alarm.
                var wsBar = marketCache.GetRecentLiveBars(snapshot.Ticker)
                    .FirstOrDefault(bar => bar.Timestamp == snapshot.Minute.Timestamp);

                if (wsBar is null)
                {
                    wsMissing++;
                    continue;
                }

                var priceOff = Math.Abs(wsBar.Close - snapshot.Minute.Close) >= PriceTolerance
                    || Math.Abs(wsBar.High - snapshot.Minute.High) >= PriceTolerance
                    || Math.Abs(wsBar.Low - snapshot.Minute.Low) >= PriceTolerance;

                var relVolumeDelta = Math.Abs(wsBar.Volume - snapshot.Minute.Volume) / Math.Max(snapshot.Minute.Volume, 1f);
                var volumeOff = relVolumeDelta >= VolumeRelativeTolerance;

                if (priceOff)
                {
                    wsPriceMismatch++;
                }
                else if (volumeOff)
                {
                    wsVolumeMismatch++;
                }
                else
                {
                    wsMatched++;
                }

                if (priceOff || volumeOff)
                {
                    mismatches.Add(new MismatchRecord(snapshot.Ticker, relVolumeDelta, wsBar, snapshot.Minute));
                }
            }

            if (spyTimestamp is not null)
            {
                memoryCache.Set(LastSpyMinuteKey, spyTimestamp.Value);
            }

            var (gapMinutesFilledFromRing, gapMinutesFilledFromRest, gapMinutesEmpty, gapTickersDeferred, gapsAssumedNatural) =
                await BackfillGaps(gaps, context.CancellationToken);

            sp.Stop();

            var dataLagSeconds = spyTimestamp is null
                ? -1
                : (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - spyTimestamp.Value) / 1000;

            var worstOffenders = string.Join(", ", mismatches
                .OrderByDescending(m => m.RelVolumeDelta)
                .Take(10)
                .Select(m => $"{m.Ticker} dv={m.RelVolumeDelta:P1} ws[o={m.WsBar.Open} h={m.WsBar.High} l={m.WsBar.Low} c={m.WsBar.Close} v={m.WsBar.Volume}] snap[o={m.SnapshotBar.Open} h={m.SnapshotBar.High} l={m.SnapshotBar.Low} c={m.SnapshotBar.Close} v={m.SnapshotBar.Volume}]"));

            logger.LogInformation(
                "SNAPSHOT_RUN tickers={tickers} appended={appended} wsMatched={wsMatched} wsPriceMismatch={wsPriceMismatch} " +
                "wsVolumeMismatch={wsVolumeMismatch} wsMissing={wsMissing} gapsDetected={gapsDetected} " +
                "gapMinutesFilledFromRing={gapMinutesFilledFromRing} gapMinutesFilledFromRest={gapMinutesFilledFromRest} " +
                "gapMinutesEmpty={gapMinutesEmpty} gapTickersDeferred={gapTickersDeferred} gapsAssumedNatural={gapsAssumedNatural} " +
                "probeAttempts={probeAttempts} probeLatencyMs={probeLatencyMs} probeExhausted={probeExhausted} spyMinuteTs={spyMinuteTs} " +
                "dataLagSeconds={dataLagSeconds} elapsedMs={elapsedMs} worstOffenders={worstOffenders}",
                snapshots.Count, appended, wsMatched, wsPriceMismatch,
                wsVolumeMismatch, wsMissing, gaps.Count,
                gapMinutesFilledFromRing, gapMinutesFilledFromRest,
                gapMinutesEmpty, gapTickersDeferred, gapsAssumedNatural, probe.Attempts,
                probe.LatencyMs, probe.Exhausted, spyTimestamp ?? 0,
                dataLagSeconds, sp.ElapsedMilliseconds, worstOffenders);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during snapshot job: {message}", ex.Message);
        }
    }

    /// <summary>
    /// Retries a cheap single-ticker SPY snapshot until its minute bar advances past
    /// the one the previous run applied. Advancement (not wall clock) is the freshness
    /// signal because the data plan may be delayed by any amount. SPY is the sentinel:
    /// per-ticker freshness is undecidable (an illiquid ticker's old bar may just mean
    /// no trades), but SPY prints every minute of extended hours.
    /// </summary>
    private async Task<(int Attempts, long LatencyMs, bool Exhausted)> ProbeForFreshBar(CancellationToken cancellationToken)
    {
        var sp = Stopwatch.StartNew();
        var lastApplied = memoryCache.TryGetValue<long>(LastSpyMinuteKey, out var value) ? value : (long?)null;

        for (var attempt = 1; attempt <= config.ProbeMaxAttempts; attempt++)
        {
            try
            {
                var probeResponse = await massiveClient.GetAllTickersSnapshot("SPY");

                var spyTimestamp = probeResponse?.Tickers?
                    .FirstOrDefault(q => q.Ticker == "SPY")?.Minute?.Timestamp;

                if (spyTimestamp is not null && (lastApplied is null || spyTimestamp > lastApplied))
                {
                    return (attempt, sp.ElapsedMilliseconds, false);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Snapshot probe attempt {attempt} failed: {message}", attempt, ex.Message);
            }

            if (attempt < config.ProbeMaxAttempts)
            {
                await Task.Delay(config.ProbeDelayMs, cancellationToken);
            }
        }

        return (config.ProbeMaxAttempts, sp.ElapsedMilliseconds, true);
    }

    /// <summary>
    /// Two-tier backfill for minute gaps: the websocket ring buffer is free and covers
    /// a single missed poll while the feed was alive; REST aggregates (capped per run)
    /// cover the rest. Minutes neither source has are counted, not treated as errors —
    /// an illiquid ticker legitimately has empty minutes.
    /// </summary>
    private async Task<(int FromRing, int FromRest, int Empty, int Deferred, int AssumedNatural)> BackfillGaps(
        List<GapRecord> gaps, CancellationToken cancellationToken)
    {
        if (gaps.Count == 0)
        {
            return (0, 0, 0, 0, 0);
        }

        var fromRing = 0;
        var assumedNatural = 0;
        List<GapRecord> stillMissing = [];

        foreach (var gap in gaps)
        {
            var ringBars = marketCache.GetRecentLiveBars(gap.Ticker);
            var inserted = ringBars.Count > 0
                ? barCacheService.BackfillMinuteBars(gap.Ticker, ringBars, gap.FromTs, gap.ToTs)
                : 0;

            fromRing += inserted;

            var expectedMinutes = (int)((gap.ToTs - gap.FromTs) / 60_000) - 1;

            if (inserted >= expectedMinutes)
            {
                continue;
            }

            // Long gaps are natural illiquidity, not missed polls; REST has nothing
            // for those minutes either, so don't burn calls on them.
            if (expectedMinutes > config.RestBackfillMaxGapMinutes)
            {
                assumedNatural++;
                continue;
            }

            stillMissing.Add(gap);
        }

        var restCandidates = stillMissing.Take(config.BackfillMaxTickers).ToList();
        var deferred = stillMissing.Count - restCandidates.Count;

        var fromRest = 0;
        var empty = 0;

        await Parallel.ForEachAsync(restCandidates,
            new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = cancellationToken },
            async (gap, ct) =>
            {
                try
                {
                    var date = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(gap.ToTs), TimeZone)
                        .ToString("yyyy-MM-dd");

                    var response = await massiveClient.GetAggregates(new MassiveAggregateRequest
                    {
                        Ticker = gap.Ticker,
                        Multiplier = 1,
                        Timespan = Timespan.minute.ToString(),
                        From = date,
                        To = date,
                        Limit = 50000
                    });

                    var bars = response?.Results?.ToList() ?? [];
                    var inserted = bars.Count > 0
                        ? barCacheService.BackfillMinuteBars(gap.Ticker, bars, gap.FromTs, gap.ToTs)
                        : 0;

                    var expectedMinutes = (int)((gap.ToTs - gap.FromTs) / 60_000) - 1;

                    Interlocked.Add(ref fromRest, inserted);
                    Interlocked.Add(ref empty, Math.Max(expectedMinutes - inserted, 0));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "REST gap backfill failed for {ticker}: {message}", gap.Ticker, ex.Message);
                }
            });

        return (fromRing, fromRest, empty, deferred, assumedNatural);
    }
}
