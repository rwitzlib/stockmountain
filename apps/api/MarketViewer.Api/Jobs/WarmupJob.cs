using MarketViewer.Api.HostedServices;
using MarketViewer.Api.Services;
using Quartz;

namespace MarketViewer.Api.Jobs;

/// <summary>
/// Runs the market cache warmup. Fired three ways, all against the same job so
/// executions never overlap: an immediate trigger at startup, a daily rebuild at
/// 3:30am ET (force=true), and a recovery trigger every 5 minutes that only acts
/// when the cache never became ready (force=false).
/// </summary>
[DisallowConcurrentExecution]
public class WarmupJob(
    MarketCacheWarmer warmer,
    CacheWarmupState warmupState,
    ILogger<WarmupJob> logger) : IJob
{
    public const string ForceKey = "force";

    public async Task Execute(IJobExecutionContext context)
    {
        var force = context.MergedJobDataMap.GetBooleanValue(ForceKey);

        if (!force && warmupState.IsReady)
        {
            return;
        }

        logger.LogInformation("Warmup job triggered (force: {force}, status: {status}).", force, warmupState.Status);

        await warmer.RunWarmupAsync(context.CancellationToken);
    }
}
