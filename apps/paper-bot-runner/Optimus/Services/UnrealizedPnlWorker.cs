using MarketViewer.Contracts.Enums.Strategy;
using Optimus.Infrastructure.Repositories;
using Quartz;

namespace Optimus.Services;

/// <summary>
/// Scheduled worker that refreshes unrealized P/L for all active strategies.
/// Runs periodically during market hours.
/// </summary>
[DisallowConcurrentExecution]
public class UnrealizedPnlWorker(
    StrategyRepository strategyRepository,
    StrategyStateRepository stateRepository,
    UnrealizedPnlService pnlService,
    ILogger<UnrealizedPnlWorker> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("Starting unrealized P/L refresh for all active strategies");

        try
        {
            // Get all strategies with open positions by querying states directly
            var allStrategies = await strategyRepository.ListAll();
            var activeStrategies = allStrategies
                .Where(s => s.State == StrategyStateType.Active)
                .ToList();

            if (activeStrategies.Count == 0)
            {
                logger.LogDebug("No active strategies to refresh");
                return;
            }

            var refreshCount = 0;
            var skippedCount = 0;
            var errorCount = 0;

            foreach (var strategy in activeStrategies)
            {
                try
                {
                    // Skip strategies that have never traded (no state record). Strategies with
                    // 0 open positions still go through RefreshUnrealizedPnl so any stale
                    // UnrealizedPnl left over from the last close gets reset to 0.
                    var state = await stateRepository.GetState(strategy.Id);
                    if (state == null)
                    {
                        skippedCount++;
                        continue;
                    }

                    var startingBalance = (decimal)strategy.PositionSettings.StartingBalance;
                    var result = await pnlService.RefreshUnrealizedPnl(strategy.Id, startingBalance);

                    if (result != null)
                    {
                        refreshCount++;
                        logger.LogDebug(
                            "Refreshed P/L for strategy {StrategyId}: unrealized={UnrealizedPnl}, total={TotalBalance}",
                            strategy.Id, result.UnrealizedPnl, result.CurrentBalance);
                    }
                    else
                    {
                        errorCount++;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error refreshing P/L for strategy {StrategyId}", strategy.Id);
                    errorCount++;
                }
            }

            logger.LogInformation(
                "Unrealized P/L refresh complete: {RefreshCount} refreshed, {SkippedCount} skipped (no state), {ErrorCount} errors",
                refreshCount, skippedCount, errorCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in UnrealizedPnlWorker");
        }
    }
}
