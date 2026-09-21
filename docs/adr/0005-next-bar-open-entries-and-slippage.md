# ADR 0005: Next-Bar-Open Entries and Modeled Slippage

## Status

Accepted (backtester implemented 2026-09-21; paper-engine entry fill deferred, see Consequences)

## Context

Plan 19 (2026-09-04) compared the live paper RSI-low strategy with its backtests and
found two fill assumptions that overstate results:

1. **Entries were priced at the signal bar's close.** Entries evaluate completed bars
   only (ADR 0003), so a signal on bar T is known at T+1:00, when T's close can no longer
   be traded. Measured on ~1,800 `rsi < 30` signals: the signal bar closes below its own
   VWAP 75% of the time, and the next bar opens +0.12% to +0.15% above the modeled fill.
   The 3-minute hold return fell from about +0.22% to about +0.09% when measured from the
   next open: roughly half the edge was fill optimism. For `crosses_over(rsi, 30)` the
   bias runs the other way (next open 0.06–0.18% below the modeled fill).
2. **Stops filled exactly at the trigger price** (or the open on a gap-through, ADR
   0004). Live stop fills over 2026-07-24 → 2026-09-04 had a median of −2.9% against a
   −2.0% stop, with 46% worse than −3%. Part of that window predates the 2026-08-17
   trigger-price fix, so the lasting gap is smaller than 0.9% but not zero: a stop sells
   into a falling bid, worst on sub-$2 low-float names.

The internal paper engine has the same entry optimism: `DefaultAdapter.Buy` fills at the
snapshot's latest minute close, which is the signal bar. Backtest and paper agreed with
each other and were both optimistic against a real broker.

### Rejected: scan a forming bar built from per-second aggregates

Considered modeling live's websocket forming bar in backtests by building the current
minute from second aggregates. Rejected because live entries do not scan the forming bar
(ADR 0003: its volume undercounts 6–12× on thin names), historical second aggregates
include the late dark-pool prints the live stream misses (so the backtest partial bar
would differ from live's in the other direction), and it multiplies data and filter
evaluations by ~60 across the universe. Second aggregates are better spent on *fill
pricing* around each trade; see Consequences.

## Decision

Backtests carry `BacktestFillSettings` (`BacktestCreateRequest.FillSettings`, threaded
through `OrchestratorRequest` and `WorkerRequest`):

1. **`EntryFill`, default `nextBarOpen`.** The entry prices at the open of the first bar
   after the signal bar. `signalClose` keeps the old model for parity studies against
   the paper engine. Entering at the open exposes the trade to the rest of that bar, so
   stops and targets can trigger on the fill bar itself.
2. **`SlippagePercent`, default 0.** Adverse, percent of price, on market fills: the
   entry (pays up) and the timed / end-of-data exit and the hindsight "high" exit (give
   up). Default 0 because next-bar-open already captures the measured entry gap.
3. **`StopSlippagePercent`, default 0.5.** Applied to the stop fill after the ADR 0004
   trigger / gap-through price. 0.5% is a middle estimate between the trigger-price
   model (0) and the measured 0.9% gap.
4. **Take-profit fills never slip.** They are limit-like: the target price, or the open
   on a gap-through.
5. Stop and target thresholds derive from the slipped entry price, because that is the
   position's actual cost basis. Both slippage values are validated to 0–10%.

**Recording.** The API and the orchestrator both resolve omitted settings to the
defaults before the run is stored, so a stored backtest always says what it ran with. A
stored backtest with **no** fill settings predates this ADR and ran with
`BacktestFillSettings.Legacy` (signal close, no slippage). The UI labels those runs
rather than hiding the difference, and copying one into the create form applies the new
defaults so a re-run shows the effect.

The scanner's S3 entry cache holds signals, not fills, so `ScannerService.CacheVersion`
does not change.

## Consequences

- Results run after this change are systematically lower than saved results for the same
  strategy, most for dip-buying entries (RSI-low family). Strategies that buy strength
  can improve slightly. Old and new runs are not comparable; re-run before comparing.
- **Backtest and the internal paper engine now disagree on entries by design.** Paper
  still fills at the signal bar's close. Follow-up: fill `DefaultAdapter.Buy` from the
  forming-bar price via `/live/prices`, as exits have done since ADR 0004, so both mean
  "price at execution time". Until then use `signalClose` for parity studies.
- 0.5% stop slippage is one number for every ticker and price band. It understates
  sub-$2 low-floats and overstates liquid large caps; users can set it per backtest.
- Follow-up worth more than tuning that number: price entries, stops and targets from
  per-second aggregates around each trade (entry at T+60s plus latency; stop at the
  second after the trigger crosses). That replaces the assumed stop slippage with a
  measured one per fill and resolves same-bar stop/target ordering, currently assumed
  worst case. Needs a check that the Massive plan serves second aggregates back to 2024.
