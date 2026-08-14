# ADR 0004: Intrabar Exits and Trigger-Price Fills

## Status

Accepted (implemented 2026-08-14)

## Context

Live paper losses ran consistently worse than the backtest predicted for the same
strategy. Three compounding causes, all in the exit path:

1. **The backtester clamped stop-loss P/L to the configured value.** `CheckStopLoss`
   found the first *close* crossing the threshold, then booked `Profit` as exactly the
   configured stop (while `EndPrice` recorded the actual crossing close — the two
   disagreed). A -5% stop always backtested as exactly -5%; live it never is. Take
   profit had the same clamp.
2. **Live paper evaluated exits on the last completed snapshot minute bar**, polled
   every 10s — up to a minute of detection lag after the (data-time) crossing.
3. **The paper fill re-fetched the snapshot after the exit decision**, so the booked
   fill drifted further from the price the decision was made against.

A real broker stop triggers on the last trade price, intrabar — not on any bar close.
For the internal paper integration there is no broker and no real-time fill: everything
runs in data time (ADR 0003, `MarketDataClock`), so the delayed plan is *not* a reason
to avoid intrabar semantics — the paper engine is the broker, and its trigger rule is
whatever we define. The forming websocket bar's prices are canonical (ADR 0003 measured
`wsPriceMismatch=0`; only volume undercounts), so it is a valid last-trade proxy even
though entries must stay on completed bars.

## Decision

1. **Live paper exits evaluate the forming websocket bar.** New internal batch endpoint
   `POST /live/prices` (`LivePricesController`) returns the forming bar's latest tick
   (ring-buffer newest-completed-bar fallback) per ticker. `SellWorker` prefers it and
   falls back to the Massive snapshot for tickers missing or stale (>5 data-minutes).
   The endpoint is guarded by a shared-secret bearer policy (`InternalAuth:Token`,
   fails closed when unset); Optimus authenticates via `INTERNAL_API_TOKEN`.
2. **Paper fills book the trigger price.** `IAdapter.Sell` now carries the price the
   exit decision was made against; `DefaultAdapter` fills at it instead of re-fetching.
   Broker adapters ignore it (real fills are ground truth). Price-less exits (timed
   exit on a halted ticker) still fall back to the snapshot.
3. **The backtester models intrabar stops with low/high triggers and gap-through
   fills.** Stop: first bar whose `Low <= stopPrice` (percent: `entry*(1-|v|/100)`;
   flat: `entry - |v|/shares`), filled at the stop price, or at the `Open` when the bar
   opens through it. Take profit mirrored on `High`/`Open`. `Profit`, `EndPrice`, and
   `EndPosition` all derive from the modeled fill — the optimistic clamp is gone. A bar
   that sweeps both thresholds books the stop (worst case), matching `ExitEvaluator`'s
   tie rule.

## Consequences

- Backtested losses can now exceed the configured stop (wick precision + gap-throughs),
  matching live. Results run after this change are systematically worse — more honest —
  than saved historical results for the same strategy; strategies tuned against clamped
  numbers will look less attractive on re-run.
- The backtest catches every intrabar wick; live sampling every 10s can miss wicks
  briefer than a tick interval — the backtest errs slightly pessimistic. Per-second
  backtest data would tighten this residual, not replace the model.
- Alpaca-integration strategies inherit the fresher evaluation price but their fills
  remain broker-reported; the delayed-data-vs-real-fill gap there is inherent and is
  properly fixed by broker-side stop orders at the threshold (backstop already exists),
  not by this change.
- Deployment requires the new shared secret on both sides: `InternalAuth__Token` (API)
  and `INTERNAL_API_TOKEN` (Optimus), wired through `var.internal_api_token` in
  `infra/tf/app`. Until both are set, exits transparently use the old snapshot path.
