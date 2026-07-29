using Alpaca.Client;
using MarketViewer.Api.HostedServices;
using MarketViewer.Api.Services;
using MarketViewer.Application.Handlers.Market.Scan;
using MarketViewer.Contracts.Caching;
using MarketViewer.Contracts.Records.Scan;
using MarketViewer.Infrastructure.Config;
using Quartz;
using ScanRequest = MarketViewer.Contracts.Requests.Market.Scan.ScanRequest;

namespace MarketViewer.Api.Jobs;

public class ScannerJob(
    ScanConfig config,
    MarketDataConfig marketDataConfig,
    ScannerCache scannerCache,
    ScanHandler scanHandler,
    SignalPublisher signalPublisher,
    MarketCalendarService marketCalendar,
    CacheWarmupState warmupState) : IJob
{
    // No new entries in the final minutes before close.
    private static readonly TimeSpan CloseBuffer = TimeSpan.FromMinutes(2);

    public async Task Execute(IJobExecutionContext context)
    {
        // Scans run on the data clock: on a delayed data plan the newest bars are
        // DelayMinutes behind the wall clock, so the session gate and the filter
        // evaluation time ("time" field) must both be shifted or time-of-day filters
        // diverge from backtests and the last minutes of the session never get
        // scanned. DelayMinutes = 0 makes this the wall clock again.
        var dataTime = DateTimeOffset.UtcNow.AddMinutes(-marketDataConfig.DelayMinutes);

        if (!warmupState.IsReady || !await marketCalendar.IsMarketOpen(CloseBuffer, dataTime))
        {
            return;
        }

        var entrySettingsList = scannerCache.GetStrategyEntrySettings(); // TODO: Use cadence parameter eventually if we want to separate jobs by cadence

        // Completed-bar mode keys the signal window to the data-clock minute: the
        // underlying data only changes once per minute, so the 15-second re-scans of
        // the same bar produce the same window and dedupe in the executor
        // (TryRecordExecution on strategy/ticker/window). Partial-bar mode keeps the
        // per-scan window since every tick evaluates fresh data.
        var window = ComputeWindow(dataTime, config.CompletedBarEntries);

        await Parallel.ForEachAsync(entrySettingsList, async (entrySettings, cancellationToken) =>
        {
            var scanRequest = new ScanRequest
            {
                Filters = entrySettings.Filters,
                Timestamp = dataTime,
                CompletedBarsOnly = config.CompletedBarEntries
            };

            var response = await scanHandler.Handle(scanRequest, cancellationToken);

            if (response.Data is null || !response.Data.Items.Any())
            {
                return;
            }

            var scanRecord = new ScanRecord
            {
                StrategyHash = entrySettings.ComputeStrategyHash(),
                Window = window,
                Tickers = response.Data.Items.Select(r => r.Ticker).ToList(),
                TimeElapsed = response.Data.TimeElapsed,
                CadenceSec = config.CadenceSec
            };

            await signalPublisher.Publish(scanRecord);
        });
    }

    public static long ComputeWindow(DateTimeOffset dataTime, bool completedBarEntries)
    {
        return completedBarEntries
            ? dataTime.ToUnixTimeSeconds() / 60 * 60
            : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
