using Alpaca.Client;
using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Records;
using MarketViewer.Contracts.Records.Strategy;
using Massive.Client.Interfaces;
using Optimus.Adapter;
using Optimus.Infrastructure.Repositories;
using Quartz;

namespace Optimus.Services;

[DisallowConcurrentExecution]
public class SellWorker(
    StrategyRepository strategyRepository,
    StrategyStateRepository stateRepository,
    TradeRepository tradeRepository,
    IMassiveClient massiveClient,
    MarketCalendarService marketCalendar,
    AdapterFactory adapterFactory,
    ILogger<SellWorker> logger) : IJob
{
    private const int SnapshotBatchSize = 500;

    public async Task Execute(IJobExecutionContext context)
    {
        if (!await marketCalendar.IsMarketOpen())
        {
            return;
        }

        try
        {
            var strategies = await strategyRepository.ListAll();
            var enabledStrategies = strategies.Where(q => q.State == StrategyStateType.Active).ToList();

            if (enabledStrategies.Count == 0)
            {
                return;
            }

            // Gather every open position up front so prices can be fetched in one batch
            // instead of one request per position.
            var openPositions = new List<(StrategyDto Strategy, TradeRecord Trade)>();
            foreach (var strategy in enabledStrategies)
            {
                var trades = await tradeRepository.ListTradesByStrategy(strategy.Id, null, TradeStatus.Open);
                openPositions.AddRange(trades.Select(trade => (strategy, trade)));
            }

            if (openPositions.Count == 0)
            {
                return;
            }

            var distinctTickers = openPositions.Select(p => p.Trade.Ticker).Distinct().ToList();
            var priceMap = await GetCurrentPrices(distinctTickers);

            var sellTasks = openPositions.Select(p => SellPositionIfApplicable(p.Strategy, p.Trade, priceMap));
            await Task.WhenAll(sellTasks);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error in SellWorker: {Message}", e.Message);
        }
    }

    /// <summary>
    /// Fetches current prices for all tickers in batched snapshot calls.
    /// Tickers missing from the result (e.g. halted) are simply absent from the map.
    /// </summary>
    private async Task<Dictionary<string, float>> GetCurrentPrices(List<string> tickers)
    {
        var prices = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        foreach (var chunk in tickers.Chunk(SnapshotBatchSize))
        {
            var response = await massiveClient.GetAllTickersSnapshot(string.Join(',', chunk));

            foreach (var snapshot in response?.Tickers ?? [])
            {
                if (snapshot.Ticker is not null && snapshot.Minute?.Close > 0)
                {
                    prices[snapshot.Ticker] = snapshot.Minute.Close;
                }
            }
        }

        if (prices.Count < tickers.Count)
        {
            logger.LogWarning("Missing prices for {MissingCount}/{TotalCount} open tickers",
                tickers.Count - prices.Count, tickers.Count);
        }

        return prices;
    }

    private async Task SellPositionIfApplicable(StrategyDto strategy, TradeRecord trade, Dictionary<string, float> priceMap)
    {
        float? currentPrice = priceMap.TryGetValue(trade.Ticker, out var price) ? price : null;

        // TODO: Evaluate ExitSettings.ConditionalExit (scan-based exits) here once supported.
        var exitReason = ExitEvaluator.Evaluate(strategy, trade, currentPrice, DateTimeOffset.Now);

        if (exitReason is null)
        {
            return;
        }

        logger.LogInformation("{ExitReason} hit for {Ticker} (strategy {StrategyId})",
            exitReason, trade.Ticker, strategy.Id);

        // The adapter persists this record on close, so the reason rides along with the fill.
        trade.ExitReason = exitReason;

        var adapter = adapterFactory.GetAdaptor(strategy.Integration);
        var sellResult = await adapter.Sell(trade);

        if (!sellResult.IsSuccess)
        {
            logger.LogWarning(
                "Sell failed for strategy {StrategyId}, ticker {Ticker}: {Reason}",
                strategy.Id, trade.Ticker, sellResult.FailureReason);
            return;
        }

        await UpdateStateOnClose(strategy, trade, sellResult);
    }

    /// <summary>
    /// Updates strategy state after a position is closed.
    /// Increments cash balance with close value, removes ticker from open set, sets cooldown.
    /// </summary>
    private async Task UpdateStateOnClose(StrategyDto strategy, TradeRecord trade, Adapter.Interfaces.SellResult sellResult)
    {
        try
        {
            // Get current state
            var startingBalance = (decimal)strategy.PositionSettings.StartingBalance;
            var state = await stateRepository.GetOrCreateState(strategy.Id, startingBalance);

            if (state == null)
            {
                logger.LogError("Failed to get state for strategy {StrategyId} during close", strategy.Id);
                return;
            }

            // Use the actual close value from the sell result
            var closeValue = sellResult.ActualCloseValue;

            // Get the entry cost of the position being closed
            var entryCost = (decimal)trade.EntryPosition;

            // Compute cooldown expiry
            var cooldownExpiry = TradeExecutionService.ComputeCooldownExpiry(strategy.PositionSettings.Cooldown);

            // Update state
            var stateUpdated = await stateRepository.UpdateStateOnClose(
                strategy.Id,
                trade.Ticker,
                closeValue,
                entryCost,
                cooldownExpiry,
                state.Version);

            if (stateUpdated)
            {
                logger.LogInformation(
                    "Updated state on close for strategy {StrategyId}, ticker {Ticker}, closeValue {CloseValue}, profit {Profit}, cooldown until {CooldownExpiry}",
                    strategy.Id, trade.Ticker, closeValue, sellResult.Profit, cooldownExpiry);

                // Write balance history snapshot after successful state update
                await WriteBalanceHistoryRecord(strategy.Id, state, closeValue, entryCost);
            }
            else
            {
                // Version mismatch - state may be inconsistent
                logger.LogWarning(
                    "State update failed on close for strategy {StrategyId}, ticker {Ticker}. " +
                    "Version mismatch, state may need reconciliation.",
                    strategy.Id, trade.Ticker);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error updating state on close for strategy {StrategyId}, ticker {Ticker}",
                strategy.Id, trade.Ticker);
        }
    }

    /// <summary>
    /// Writes a balance history snapshot after a position is closed.
    /// Uses the updated state values after the close.
    /// </summary>
    private async Task WriteBalanceHistoryRecord(string strategyId, StrategyStateRecord previousState, decimal closeValue, decimal closedEntryCost)
    {
        try
        {
            // Calculate the new values after the close
            // Note: The state update already happened, so we compute what the new values are
            var newCashBalance = previousState.CashBalance + closeValue;
            var newTotalEntryCost = previousState.TotalEntryCost - closedEntryCost;
            var newOpenPositionsCount = previousState.OpenPositionsCount - 1;

            // PositionValue = TotalEntryCost + UnrealizedPnl
            // UnrealizedPnl will be updated separately by the P/L refresh job
            var newPositionValue = newTotalEntryCost + previousState.UnrealizedPnl;
            var newCurrentBalance = newCashBalance + newPositionValue;

            var history = new BalanceHistoryRecord
            {
                StrategyId = strategyId,
                Date = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"),
                CashBalance = newCashBalance,
                TotalEntryCost = newTotalEntryCost,
                UnrealizedPnl = previousState.UnrealizedPnl, // Will be updated separately
                PositionValue = newPositionValue,
                CurrentBalance = newCurrentBalance,
                OpenPositionsCount = newOpenPositionsCount,
                RecordedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                SnapshotType = "close"
            };

            var success = await stateRepository.WriteBalanceHistoryRecord(history);

            if (success)
            {
                logger.LogDebug(
                    "Wrote balance history for strategy {StrategyId}, date {Date}, balance {Balance}",
                    strategyId, history.Date, history.CurrentBalance);
            }
            else
            {
                logger.LogWarning(
                    "Failed to write balance history for strategy {StrategyId} on {Date}",
                    strategyId, history.Date);
            }
        }
        catch (Exception ex)
        {
            // Balance history write failure is non-critical
            logger.LogError(ex,
                "Error writing balance history for strategy {StrategyId}",
                strategyId);
        }
    }
}
