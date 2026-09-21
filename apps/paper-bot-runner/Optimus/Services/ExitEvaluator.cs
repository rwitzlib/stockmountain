using MarketViewer.Contracts.Dtos;
using MarketViewer.Contracts.Enums;
using MarketViewer.Contracts.Enums.Backtest;
using MarketViewer.Contracts.Models.Strategy;
using MarketViewer.Contracts.Records;
using Optimus.Infrastructure.Utilities;

namespace Optimus.Services;

/// <summary>
/// Pure exit decision logic for an open position. Evaluation order is timed exit, then
/// the stops (fixed and trailing — the higher one is what price reaches first), then
/// take profit — a same-tick tie goes to the stop, matching the backtester's same-bar
/// semantics. Reasons use the shared BacktestExitReason vocabulary so live trades and
/// backtest results report exits identically.
/// </summary>
public static class ExitEvaluator
{
    /// <summary>
    /// Returns the exit reason for the position, or null to keep holding.
    /// <paramref name="currentPrice"/> may be null (e.g. halted ticker); the timed exit
    /// still applies, price-based exits are skipped.
    /// The trailing stop is priced from <see cref="TradeRecord.HighWaterMark"/> as
    /// persisted before this tick — the caller ratchets the mark afterwards — so a tick
    /// never tightens the stop it is being tested against, matching the backtester.
    /// <paramref name="session"/> carries today's close and the next session's open: a
    /// timed exit projected to land between them can never fire on its own clock (the
    /// worker only runs during the session), so it is pulled forward to the session's
    /// final minute — the same bar the backtester fills these exits on. Omitting it
    /// keeps the raw projected exit, which then fires at the next open as a backstop.
    /// </summary>
    public static BacktestExitReason? Evaluate(StrategyDto strategy, TradeRecord trade, float? currentPrice, DateTimeOffset now,
        (DateTimeOffset Close, DateTimeOffset NextOpen)? session = null)
    {
        var timedExit = strategy.ExitSettings?.TimedExit;
        if (timedExit?.Timeframe is not null)
        {
            var projectedExitDate = DateUtilities.GetEndDate(DateTimeOffset.Parse(trade.OpenedAt), timedExit.Timeframe);

            if (session is { } s && projectedExitDate >= s.Close && projectedExitDate < s.NextOpen)
            {
                projectedExitDate = s.Close.AddMinutes(-1);
            }

            if (projectedExitDate <= now)
            {
                return BacktestExitReason.timedExit;
            }
        }

        if (currentPrice is null or <= 0)
        {
            return null;
        }

        var currentPosition = currentPrice.Value * trade.Shares;

        var fixedStopHit = IsThresholdHit(strategy.ExitSettings?.StopLoss, currentPosition, trade.EntryPosition, isStop: true);
        var trailingStopPrice = TrailingStopPrice(strategy, trade);
        var trailingStopHit = trailingStopPrice is not null && currentPrice.Value <= trailingStopPrice;

        if (fixedStopHit || trailingStopHit)
        {
            // Both stops crossed on one tick: attribute the exit to the higher stop, the
            // one price fell through first. The fixed stop's price is implied by its
            // threshold, so compare via "is the trail above the fixed stop's level".
            var fixedStopPrice = FixedStopPrice(strategy.ExitSettings?.StopLoss, trade);

            var trailingWins = trailingStopHit
                && (!fixedStopHit || fixedStopPrice is null || trailingStopPrice >= fixedStopPrice);

            return trailingWins ? BacktestExitReason.trailingStop : BacktestExitReason.stopLoss;
        }

        if (IsThresholdHit(strategy.ExitSettings?.TakeProfit, currentPosition, trade.EntryPosition, isStop: false))
        {
            return BacktestExitReason.takeProfit;
        }

        return null;
    }

    /// <summary>
    /// The trailing stop's resting price for the trade, or null when the strategy has no
    /// trailing stop or it has not armed yet. A record with no persisted high-water mark
    /// (never above entry, or predating the field) trails from the entry price.
    /// </summary>
    public static float? TrailingStopPrice(StrategyDto strategy, TradeRecord trade)
    {
        var highWaterMark = Math.Max(trade.HighWaterMark ?? 0, trade.EntryPrice);

        return TrailingStopMath.StopPrice(strategy.ExitSettings?.TrailingStop, trade.EntryPrice, trade.Shares, highWaterMark);
    }

    private static float? FixedStopPrice(MarketViewer.Contracts.Models.Strategy.Exit stopLoss, TradeRecord trade)
    {
        if (stopLoss is null || trade.Shares <= 0)
        {
            return null;
        }

        return stopLoss.Type switch
        {
            ExitValueType.percent => trade.EntryPrice * (1 - Math.Abs(stopLoss.Value) / 100),
            ExitValueType.flat => trade.EntryPrice - Math.Abs(stopLoss.Value) / trade.Shares,
            _ => null
        };
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
