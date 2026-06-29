using MarketViewer.Contracts.Caching;
using MarketViewer.Core.Services;
using Quartz;

namespace MarketViewer.Api.Jobs;

public class PopulateScannerJob(IStrategyRepository repository, ScannerCache scannerCache, ILogger<PopulateScannerJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("Populating scanner cache with active strategies.");

        var entrySettingsList = await repository.ListUniqueActiveStrategies();

        scannerCache.ReplaceAll(entrySettingsList);

        logger.LogInformation("Populated scanner cache with {Count} active strategies.", entrySettingsList.Count());
    }
}
