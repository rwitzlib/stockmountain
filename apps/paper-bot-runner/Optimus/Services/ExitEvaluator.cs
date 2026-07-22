using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Records;
using Optimus.Infrastructure.Utilities;

namespace Optimus.Services;

public enum ExitReason
{
    TimedExit,
    StopLoss,
    TakeProfit
}

/// <summary>
/// Pure exit decision logic for an open position. Evaluation order is timed exit, then
/// stop loss, then take profit — a same-tick tie goes to the stop, matching the
/// backtester's same-bar semantics.
/// </summary>
public static class ExitEvaluator
{
    /// <summary>
    /// Returns the exit reason for the position, or null to keep holding.
    /// <paramref name="currentPrice"/> may be null (e.g. halted ticker); the timed exit
    /// still applies, price-based exits are skipped.
    /// </summary>
    public static ExitReason? Evaluate(StrategyDto strategy, TradeRecord trade, float? currentPrice, DateTimeOffset now)
    {
        var timedExit = strategy.ExitSettings?.TimedExit;
        if (timedExit?.Timeframe is not null)
        {
            var projectedExitDate = DateUtilities.GetEndDate(DateTimeOffset.Parse(trade.OpenedAt), timedExit.Timeframe);
            if (projectedExitDate <= now)
            {
                return ExitReason.TimedExit;
            }
        }

        if (currentPrice is null or <= 0)
        {
            return null;
        }

        var currentPosition = currentPrice.Value * trade.Shares;

        if (IsThresholdHit(strategy.ExitSettings?.StopLoss, currentPosition, trade.EntryPosition, isStop: true))
        {
            return ExitReason.StopLoss;
        }

        if (IsThresholdHit(strategy.ExitSettings?.TakeProfit, currentPosition, trade.EntryPosition, isStop: false))
        {
            return ExitReason.TakeProfit;
        }

        return null;
    }

    private static bool IsThresholdHit(MarketViewer.Contracts.Models.Strategy.Exit exit, float currentPosition, float entryPosition, bool isStop)
    {
        if (exit is null || entryPosition == 0)
        {
            return false;
        }

        var change = exit.Type switch
        {
            ExitValueType.flat => currentPosition - entryPosition,
            ExitValueType.percent => (currentPosition - entryPosition) / entryPosition * 100,
            _ => (float?)null
        };

        if (change is null)
        {
            return false;
        }

        // A stop loss is always a loss and a take profit always a gain, regardless of the
        // sign the user entered — same normalization as the backtester's CheckStopLoss/CheckTakeProfit.
        var threshold = isStop ? -Math.Abs(exit.Value) : Math.Abs(exit.Value);

        return isStop ? change <= threshold : change >= threshold;
    }
}
