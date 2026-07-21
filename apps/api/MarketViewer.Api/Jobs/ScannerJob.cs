using Alpaca.Client;
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
    ScannerCache scannerCache,
    ScanHandler scanHandler,
    SignalPublisher signalPublisher,
    MarketCalendarService marketCalendar) : IJob
{
    // No new entries in the final minutes before close.
    private static readonly TimeSpan CloseBuffer = TimeSpan.FromMinutes(2);

    public async Task Execute(IJobExecutionContext context)
    {
        if (!await marketCalendar.IsMarketOpen(CloseBuffer))
        {
            return;
        }

        var entrySettingsList = scannerCache.GetStrategyEntrySettings(); // TODO: Use cadence parameter eventually if we want to separate jobs by cadence

        await Parallel.ForEachAsync(entrySettingsList, async (entrySettings, cancellationToken) =>
        {
            var scanRequest = new ScanRequest
            {
                Filters = entrySettings.Filters
            };

            var response = await scanHandler.Handle(scanRequest, cancellationToken);

            if (response.Data is null || !response.Data.Items.Any())
            {
                return;
            }

            var scanRecord = new ScanRecord
            {
                StrategyHash = entrySettings.ComputeStrategyHash(),
                Window = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Tickers = response.Data.Items.Select(r => r.Ticker).ToList(),
                TimeElapsed = response.Data.TimeElapsed,
                CadenceSec = config.CadenceSec
            };

            await signalPublisher.Publish(scanRecord);
        });
    }
}
