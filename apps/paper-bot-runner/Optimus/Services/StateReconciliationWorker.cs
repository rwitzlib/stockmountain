using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Enums.Strategy;
using Optimus.Infrastructure.Repositories;
using Quartz;

namespace Optimus.Services;

/// <summary>
/// Rebuilds each active strategy's state (cash, entry cost, open positions/tickers) from its
/// trade records and overwrites the state when it has drifted. Runs once at startup and daily
/// after market close. The overwrite is version-conditioned, so a trade executing mid-reconcile
/// wins and the strategy is simply retried on the next run. Cooldowns and unrealized P/L are
/// left untouched.
/// </summary>
[DisallowConcurrentExecution]
public class StateReconciliationWorker(
    StrategyRepository strategyRepository,
    StrategyStateRepository stateRepository,
    TradeRepository tradeRepository,
    ILogger<StateReconciliationWorker> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var strategies = await strategyRepository.ListAll();
            var activeStrategies = strategies.Where(s => s.State == StrategyStateType.Active).ToList();

            var reconciled = 0;
            foreach (var strategy in activeStrategies)
            {
                try
                {
                    if (await ReconcileStrategy(strategy))
                    {
                        reconciled++;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error reconciling state for strategy {StrategyId}", strategy.Id);
                }
            }

            logger.LogInformation(
                "State reconciliation pass complete: {Reconciled}/{Total} strategies corrected",
                reconciled, activeStrategies.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in StateReconciliationWorker: {Message}", ex.Message);
        }
    }

    /// <summary>Returns true when drift was found and the corrected state was written.</summary>
    private async Task<bool> ReconcileStrategy(StrategyDto strategy)
    {
        var startingBalance = (decimal)strategy.PositionSettings.StartingBalance;
        var state = await stateRepository.GetOrCreateState(strategy.Id, startingBalance);

        if (state == null)
        {
            logger.LogError("Failed to get state for strategy {StrategyId} during reconciliation", strategy.Id);
            return false;
        }

        var trades = await tradeRepository.ListTradesByStrategy(strategy.Id);
        var expected = StateReconciler.Compute(startingBalance, trades);

        if (!StateReconciler.HasDrift(state, expected))
        {
            return false;
        }

        logger.LogWarning(
            "State drift for strategy {StrategyId}: cash {StateCash} -> {ExpectedCash}, " +
            "entryCost {StateEntryCost} -> {ExpectedEntryCost}, openPositions {StateOpen} -> {ExpectedOpen}, " +
            "openTickers [{StateTickers}] -> [{ExpectedTickers}]",
            strategy.Id,
            state.CashBalance, expected.CashBalance,
            state.TotalEntryCost, expected.TotalEntryCost,
            state.OpenPositionsCount, expected.OpenPositionsCount,
            string.Join(",", state.OpenTickers), string.Join(",", expected.OpenTickers));

        var written = await stateRepository.TryOverwriteReconciledState(
            strategy.Id,
            expected.CashBalance,
            expected.TotalEntryCost,
            expected.OpenPositionsCount,
            expected.OpenTickers,
            state.Version);

        if (written)
        {
            logger.LogInformation("Reconciled state for strategy {StrategyId}", strategy.Id);
        }

        return written;
    }
}
