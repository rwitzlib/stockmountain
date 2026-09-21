# ADR 0006: Trailing Stop Exit

## Status

Accepted (implemented 2026-09-21)

## Context

Every exit was anchored to the entry price: a fixed stop, a fixed target, and a timed
exit. Plan 19's review of the best live strategy (RSI-low mean reversion) found that the
fat right tail — the minority of trades that keep running after the reversion — was being
cut by the fixed target or the timed exit at an arbitrary point (plan 19 A4, C2). Plan 11
#6 listed a trailing stop as the natural exit for breakout and momentum variants, and
the only exit type that can hold a runner while keeping the tight-risk profile.

The stop needs to mean the same thing in the backtester and live paper trading, which
already share intrabar stop semantics (ADR 0004: trigger on the low, fill at the stop or
the gap-through open, stop slippage on top).

## Decision

1. **A new optional `StrategyExitSettings.TrailingStop { Type, Value, Activation? }`.**
   `Value` is the trail distance below the position's high-water mark: percent of the
   mark, or position dollars (`flat`), matching the fixed `Exit` convention. `Activation`
   is the gain over entry (same units) the mark must reach before the trail arms; null or
   0 trails from entry. The stop only ratchets up. The fixed stop loss remains mandatory
   and runs alongside: the two are resting sell stops on the same position, and whichever
   sits higher is the one price reaches first. Below the activation gain the fixed stop
   bounds the loss; once the trail rises past it, the trail takes over. Validation
   rejects a zero/negative distance and a negative activation.
2. **The high-water mark excludes the bar or tick being tested.** In the backtester
   (`WorkerFunction.CheckTrailingStop`) each bar's low is tested against the stop implied
   by the highs of the bars before it, then the bar's high ratchets the mark. Within one
   bar the order of high and low is unknown, so a bar never tightens the stop it triggers.
   Live (`ExitEvaluator`) evaluates a tick against the mark persisted from earlier ticks,
   then `SellWorker` ratchets. Both fill like the fixed stop: the stop price, or the open
   on a gap-through, then stop slippage (ADR 0005).
3. **Live persists the mark on the trade record.** `TradeRecord.HighWaterMark` is raised
   by a conditional `UpdateItem` (`TradeRepository.RaiseHighWaterMark`: only while the
   trade is open, only upward) rather than a whole-record `Put`, so a manual close landing
   between the worker's read and its write is never overwritten back to open. Null means
   "never above entry" and trails from the entry price, which also covers records that
   predate the field. Only strategies with a trailing stop pay for the write, and only on
   a tick that raised the mark.
4. **New exit reason `trailingStop`** in the shared `BacktestExitReason` vocabulary. On a
   same-bar tie between the fixed and trailing stop the one with the higher fill price is
   attributed (it fired first); a tie with the take profit still goes to the stop
   (worst case, ADR 0004). Shared payloads carry `HasTrailingStop` when masked.

The shared arithmetic lives in `MarketViewer.Contracts.Models.Strategy.TrailingStopMath`
so the backtester and Optimus cannot drift.

## Consequences

- Strategies can hold runners: the fixed target can be set wide (or effectively off) and
  the trail decides the exit. Existing strategies are unaffected — the field is optional
  and absent on every stored record.
- Backtests are slightly pessimistic relative to a per-tick trail: a bar whose high comes
  before its low would, live, have tightened the stop before the dip. The residual is one
  bar's range and errs against the strategy, consistent with ADR 0004's direction.
- Live sampling every 10s can miss a brief high, so the live mark can sit below the
  backtest's bar-high mark; the live trail then rests slightly lower. Same class of
  residual as ADR 0004's missed wicks.
- The Alpaca backstop order is unchanged: it stays the disaster stop far below the
  logical fixed stop. A broker-side trailing-stop order (Alpaca supports `trailing_stop`)
  would remove the sampling residual for that tier and is the natural follow-up before
  the live tier goes on (plan 08 phase 4).
- The trailing stop is a fourth exit card in the create/edit forms, off by default; the
  seed when switched on is 2% trail arming at +2%.
