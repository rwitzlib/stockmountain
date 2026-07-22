using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Records.Strategy;
using Optimus.Adapter;
using Optimus.Infrastructure.Repositories;

namespace Optimus.Services;

public class TradeExecutionService(
    ExecutionDedupRepository dedupRepository,
    StrategyStateRepository stateRepository,
    AdapterFactory adaptorFactory,
    ILogger<TradeExecutionService> logger)
{
    /// <summary>
    /// Executes a buy for the given strategy and ticker if not already executed (idempotent).
    /// Uses a reserve-execute-rollback pattern to prevent race conditions.
    /// Returns true if buy was executed, false if skipped (duplicate, validation failure, or ineligible).
    /// </summary>
    public async Task<bool> ExecuteBuyIfNotDuplicate(StrategyDto strategy, string ticker, long window)
    {
        // 1. Check idempotency via conditional write
        var isFirstExecution = await dedupRepository.TryRecordExecution(strategy.Id, ticker, window);

        if (!isFirstExecution)
        {
            logger.LogInformation(
                "Skipping duplicate execution for strategy {StrategyId}, ticker {Ticker}, window {Window}",
                strategy.Id, ticker, window);
            return false;
        }

        // 2. Ensure state exists (lazy initialization)
        var startingBalance = (decimal)strategy.PositionSettings.StartingBalance;
        var state = await stateRepository.GetOrCreateState(strategy.Id, startingBalance);

        if (state == null)
        {
            logger.LogError("Failed to get or create state for strategy {StrategyId}", strategy.Id);
            return false;
        }

        // 3. Check cooldown (this is the only eligibility check that can be done before reservation)
        var cooldownResult = CheckCooldown(strategy, ticker, state);
        if (!cooldownResult.IsEligible)
        {
            logger.LogInformation(
                "Buy not eligible for strategy {StrategyId}, ticker {Ticker}: {Reason}",
                strategy.Id, ticker, cooldownResult.Reason);
            return false;
        }

        // 4. ATOMIC: Reserve position slot (checks funds, max positions, and ticker not already open)
        // This is the critical section - all eligibility checks and state mutation happen atomically
        var positionCost = (decimal)strategy.PositionSettings.Model.Size;
        var settings = strategy.PositionSettings;

        var reservationResult = await stateRepository.TryReservePosition(
            strategy.Id,
            ticker,
            positionCost,
            settings.MaxConcurrentPositions,
            settings.AllowSimultaneous);

        if (!reservationResult.IsSuccess)
        {
            logger.LogInformation(
                "Position reservation failed for strategy {StrategyId}, ticker {Ticker}: {Reason}",
                strategy.Id, ticker, reservationResult.FailureReason);
            return false;
        }

        logger.LogDebug(
            "Position reserved for strategy {StrategyId}, ticker {Ticker}, version {Version}",
            strategy.Id, ticker, reservationResult.NewVersion);

        // 5. Execute buy via adapter
        var adapter = adaptorFactory.GetAdaptor(strategy.Integration);

        try
        {
            var buyResult = await adapter.Buy(strategy, ticker);

            if (!buyResult.IsSuccess)
            {
                logger.LogWarning(
                    "Buy execution failed for strategy {StrategyId}, ticker {Ticker}: {Reason}. Rolling back reservation.",
                    strategy.Id, ticker, buyResult.FailureReason);

                // Rollback the reservation since trade failed
                await stateRepository.RollbackPositionReservation(
                    strategy.Id,
                    ticker,
                    positionCost,
                    reservationResult.NewVersion);

                return false;
            }

            logger.LogInformation(
                "Successfully executed buy for strategy {StrategyId}, ticker {Ticker}, tradeId {TradeId}, actualCost {ActualCost}",
                strategy.Id, ticker, buyResult.TradeId, buyResult.ActualEntryCost);

            // 6. Adjust cash balance if actual cost differs from reserved amount
            // This handles the case where shares * price != position.Size (which is almost always)
            if (buyResult.ActualEntryCost != positionCost)
            {
                await stateRepository.AdjustPositionCost(
                    strategy.Id,
                    positionCost,
                    buyResult.ActualEntryCost);
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing buy for strategy {StrategyId}, ticker {Ticker}. Rolling back reservation.",
                strategy.Id, ticker);

            // Rollback the reservation since trade failed
            try
            {
                await stateRepository.RollbackPositionReservation(
                    strategy.Id,
                    ticker,
                    positionCost,
                    reservationResult.NewVersion);
            }
            catch (Exception rollbackEx)
            {
                logger.LogError(rollbackEx,
                    "Failed to rollback reservation for strategy {StrategyId}, ticker {Ticker}. Manual reconciliation may be needed.",
                    strategy.Id, ticker);
            }

            return false;
        }
    }

    /// <summary>
    /// Checks if the ticker is in cooldown.
    /// </summary>
    private BuyEligibilityResult CheckCooldown(StrategyDto strategy, string ticker, StrategyStateRecord state)
    {
        if (state.Cooldowns.TryGetValue(ticker, out var cooldownExpiry))
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now < cooldownExpiry)
            {
                var remainingSeconds = cooldownExpiry - now;
                return BuyEligibilityResult.NotEligible(
                    $"Ticker {ticker} is in cooldown for {remainingSeconds} more seconds");
            }
        }

        return BuyEligibilityResult.Eligible();
    }

    /// <summary>
    /// Computes cooldown expiry in Unix seconds based on the Timeframe setting.
    /// </summary>
    public static long ComputeCooldownExpiry(MarketViewer.Contracts.Models.Timeframe cooldown)
    {
        if (cooldown == null)
        {
            return 0;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var seconds = cooldown.Timespan switch
        {
            Timespan.minute => cooldown.Multiplier * 60,
            Timespan.hour => cooldown.Multiplier * 3600,
            Timespan.day => cooldown.Multiplier * 86400,
            Timespan.week => cooldown.Multiplier * 604800,
            Timespan.month => cooldown.Multiplier * 2592000, // 30 days
            Timespan.quarter => cooldown.Multiplier * 7776000, // 90 days
            Timespan.year => cooldown.Multiplier * 31536000, // 365 days
            _ => 0
        };

        return now + seconds;
    }
}

/// <summary>
/// Result of buy eligibility check.
/// </summary>
public class BuyEligibilityResult
{
    public bool IsEligible { get; private set; }
    public string Reason { get; private set; }

    private BuyEligibilityResult() { }

    public static BuyEligibilityResult Eligible() => new() { IsEligible = true };

    public static BuyEligibilityResult NotEligible(string reason) => new()
    {
        IsEligible = false,
        Reason = reason
    };
}
