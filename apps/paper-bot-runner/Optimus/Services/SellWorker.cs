using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Enums.Strategy;
using MarketViewer.Contracts.Records;
using MarketViewer.Contracts.Records.Strategy;
using MarketViewer.Contracts.Requests.Market;
using MarketViewer.Contracts.Responses.Market;
using Optimus.Adapter;
using Optimus.Infrastructure.Repositories;
using Optimus.Utilities;
using Quartz;

namespace Optimus.Services;

public class SellWorker(
    UserRepository userRepository,
    StrategyRepository strategyRepository,
    StrategyStateRepository stateRepository,
    TradeRepository tradeRepository,
    HttpClient httpClient,
    AdapterFactory adapterFactory,
    ILogger<SellWorker> logger) : IJob
{
    private static readonly TimeSpan Offset = TimeZoneInfo.FindSystemTimeZoneById("America/New_York")
        .IsDaylightSavingTime(DateTimeOffset.Now.Date) ? TimeSpan.FromHours(-4) : TimeSpan.FromHours(-5);

    private readonly DateTimeOffset MarketOpen = new(
        DateTimeOffset.Now.Year, DateTimeOffset.Now.Month, DateTimeOffset.Now.Day, 9, 30, 0, Offset);

    private readonly DateTimeOffset MarketClose = new(
        DateTimeOffset.Now.Year, DateTimeOffset.Now.Month, DateTimeOffset.Now.Day, 20, 58, 0, Offset);

    public async Task Execute(IJobExecutionContext context)
    {
        // Skip weekends
        if (DateTimeOffset.Now.DayOfWeek == DayOfWeek.Saturday || DateTimeOffset.Now.DayOfWeek == DayOfWeek.Sunday)
        {
            return;
        }

        // Skip outside market hours
        if (DateTimeOffset.Now < MarketOpen || DateTimeOffset.Now > MarketClose)
        {
            return;
        }

        try
        {
            var strategies = await strategyRepository.ListAll();
            var enabledStrategies = strategies.Where(q => q.State == StrategyStateType.Active).ToList();

            var tasks = new List<Task>();
            foreach (var strategy in enabledStrategies)
            {
                tasks.Add(CheckAndExecuteSells(strategy));
            }

            await Task.WhenAll(tasks);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error in SellWorker: {Message}", e.Message);
        }
    }

    private async Task CheckAndExecuteSells(StrategyDto strategy)
    {
        var openPositions = await tradeRepository.ListTradesByStrategy(strategy.Id, null, TradeStatus.Open);

        var sellTasks = new List<Task>();
        foreach (var position in openPositions)
        {
            sellTasks.Add(SellPositionIfApplicable(strategy, position));
        }

        await Task.WhenAll(sellTasks);
    }

    private async Task SellPositionIfApplicable(StrategyDto strategy, TradeRecord trade)
    {
        var adapter = adapterFactory.GetAdaptor(strategy.Integration);

        if (await ShouldSell(strategy, trade))
        {
            var sellResult = await adapter.Sell(trade);

            if (!sellResult.IsSuccess)
            {
                logger.LogWarning(
                    "Sell failed for strategy {StrategyId}, ticker {Ticker}: {Reason}",
                    strategy.Id, trade.Ticker, sellResult.FailureReason);
                return;
            }

            // Update strategy state after successful sell
            await UpdateStateOnClose(strategy, trade, sellResult);
        }
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

    private async Task<bool> ShouldSell(StrategyDto strategy, TradeRecord trade)
    {
        // Check timed exit
        var projectedExitDate = DateUtilities.GetEndDate(
            DateTimeOffset.Parse(trade.OpenedAt),
            strategy.ExitSettings.TimedExit.Timeframe);

        if (projectedExitDate <= DateTimeOffset.Now)
        {
            logger.LogInformation("Timed exit hit for {Ticker}", trade.Ticker);
            return true;
        }

        // Check stop loss / take profit
        return await CheckStopLossOrTakeProfit(strategy, trade);
    }

    private async Task<bool> CheckStopLossOrTakeProfit(StrategyDto strategy, TradeRecord position)
    {
        try
        {
            var stocksResponse = await httpClient.PostAsJsonAsync("api/stocks", new StocksRequest
            {
                Ticker = position.Ticker,
                Multiplier = 1,
                Timespan = Timespan.minute,
                From = DateTimeOffset.Now.AddDays(-1),
                To = DateTimeOffset.Now
            });

            if (!stocksResponse.IsSuccessStatusCode)
            {
                logger.LogError("Error getting price for {Ticker}", position.Ticker);
                return false;
            }

            var response = await stocksResponse.Content.ReadFromJsonAsync<StocksResponse>();

            if (response?.Results == null || !response.Results.Any() || position == null)
            {
                return false;
            }

            var currentPrice = response.Results.Last().Close;
            var currentPosition = currentPrice * position.Shares;

            var stopLossHit = strategy.ExitSettings.StopLoss.Type switch
            {
                ExitValueType.flat => currentPosition - position.EntryPosition <= strategy.ExitSettings.StopLoss.Value,
                ExitValueType.percent => (currentPosition - position.EntryPosition) / position.EntryPosition * 100 <= strategy.ExitSettings.StopLoss.Value,
                _ => false
            };

            var profitTargetHit = strategy.ExitSettings.TakeProfit.Type switch
            {
                ExitValueType.flat => currentPosition - position.EntryPosition >= strategy.ExitSettings.TakeProfit.Value,
                ExitValueType.percent => (currentPosition - position.EntryPosition) / position.EntryPosition * 100 >= strategy.ExitSettings.TakeProfit.Value,
                _ => false
            };

            if (stopLossHit)
            {
                logger.LogInformation("Stop Loss hit for {Ticker}", position.Ticker);
                return true;
            }

            if (profitTargetHit)
            {
                logger.LogInformation("Take Profit hit for {Ticker}", position.Ticker);
                return true;
            }

            return false;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error checking stop loss/take profit for {Ticker}: {Message}",
                position.Ticker, e.Message);
            return false;
        }
    }
}
