# ADR 0003: Dark-Pool Volume Undercount and Completed-Bar Entries

## Status

Accepted (completed-bar entries implemented; trades-feed ingestion deferred)

## Context

The plan-10 rollover diff (SNAPSHOT_RUN wide event) compares each completed minute bar
built from the websocket per-second aggregate stream against the snapshot bar for the
same minute. First production data (2026-07-28, delayed plan):

- `wsPriceMismatch=0` — websocket OHLC matches the canonical bars essentially exactly.
- `wsVolumeMismatch=2748` vs `wsMatched=361` — the majority of bars mismatch on volume
  only, with thin names off by **6–12×** (e.g. PSEC ws=100 vs snap=1223; identical OHLC,
  single-print bars).

Root cause: off-exchange/TRF (dark pool) and odd-lot prints. They are folded into
canonical minute-bar volume but are excluded from updating OHLC by condition-code rules
(hence perfect price agreement), and they are reported late (TRF allows up to ~10s), so
the per-second websocket stream largely misses them. In thin names, dark-pool share is
often the majority of volume.

### Implications

1. The **in-progress websocket bar undercounts volume structurally** — every volume or
   relative-volume filter evaluated intra-minute is biased low, for every strategy. The
   bias direction is conservative for `volume > X` filters (misses entries, never false
   positives) but it diverges from the backtest, which only ever sees canonical bars.
2. Completed bars are already corrected: the snapshot replaces/append the canonical bar
   within seconds of minute close. The bias exists **only in the partial bar** (and in
   the ring-buffer bar for the few seconds before the snapshot lands).
3. This validated the snapshot-as-authority design and is the decisive argument for
   entries evaluating **completed bars only**.

## Decision

1. **Entries evaluate completed bars only** (implemented alongside this ADR):
   `ScannerJob` sets `CompletedBarsOnly` on scan requests (config
   `ScanConfig.CompletedBarEntries`, default on). `ScanHandler` then omits the
   in-progress live bar; websocket ring-buffer bars (completed minutes awaiting the
   snapshot) are still spliced in so entries fire seconds after a minute closes rather
   than waiting for the snapshot poll. The signal window becomes the data-clock minute
   instead of the 15-second scan tick, so re-scans of the same minute dedupe naturally
   in the executor (`TryRecordExecution` on strategy/ticker/window).
   - Residual, accepted: for the few seconds before the snapshot lands, the newest
     completed bar is the websocket (lit-volume) version — a `volume >` filter may pass
     one scan tick later than canonical data would allow, never earlier.
2. **Do not ingest the trades feed for now.** Deferred, not rejected.

## Options considered for exact live volume (deferred)

Ranked by effort; revisit if intra-minute volume-triggered entries become a wanted edge:

1. **Measure whether `av` (accumulated day volume on A events) self-corrects** — one day
   of logging `av` vs the snapshot day bar answers whether day-level/relative-volume
   filters could trust the stream. Cheap experiment; do this first.
2. **Per-ticker correction factor** — the SNAPSHOT_RUN diff already measures
   `snapshot_vol / ws_vol` per ticker per minute; dark-pool share is sticky intraday, so
   a rolling ratio could scale partial-bar volume. Statistical, zero new infrastructure.
3. **Watchlist-scoped trades feed** (`T.TICKER` for tickers passing cheap scalar
   filters) — accurate to ~10s (TRF reporting latency floor), needs the higher
   subscription tier, condition-code matrix implementation, and its own diff validation.
4. **Full `T.*` ingestion** — architecturally "correct" (it is how canonical aggregates
   are built) but tens of thousands of messages/sec at the open, a rework of the feed
   consumer, and still bounded by the ~10s TRF reporting delay. Not justified while the
   bias only affects the partial bar and entries use completed bars.

## Consequences

- Live entry decisions consume the same bar values the backtester consumes; remaining
  live-vs-backtest entry divergence comes from timing/semantics (signal latency of one
  scan tick after minute close, dedup/ordering differences, position-limit races), not
  from data content.
- Exits are the next parity frontier: the exit path still evaluates live prices.
- Ad-hoc API scans (`/api/scan` without the flag) keep the current include-partial-bar
  behavior — the web UI's interactive scans are unaffected.
