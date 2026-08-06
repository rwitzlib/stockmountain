# Plan 12 — Unify backtest & strategy create forms on the camelCase strategy model

## Context

Backtests and strategies are the same entity on the backend: `BacktestCreateRequest`
(packages/marketviewer-contracts/MarketViewer.Contracts/Requests/Market/Backtest/BacktestCreateRequest.cs)
is composed of the exact same `StrategyPositionSettings` / `StrategyExitSettings` /
`StrategyEntrySettings` models the strategy CRUD uses (`StrategyCreateRequest` /
`StrategyUpdateRequest`), plus `start`/`end` dates.

The frontend diverged. Two parallel implementations exist:

| | Strategy editor (current, correct) | Backtest create page (out of date) |
|---|---|---|
| Page | `apps/web/src/pages/optimus/StrategyEditorPage.tsx` | `apps/web/src/pages/BacktestCreatePage.tsx` (~720 lines) |
| Form UI | Composes `EntrySettingsForm` / `ExitSettingsForm` / `PositionSettingsForm` from `components/forms/strategy/` | Hand-rolled inline, ~20 `useState` hooks |
| Types | camelCase `types/strategy.ts` (`startingBalance`, `takeProfit`…) | PascalCase `types/backtest.ts` request shapes (`StartingBalance`, `TakeProfit`…) |
| Exit type enum | `'percent' \| 'flat'` — matches backend `ExitValueType` | `'percent' \| 'value'` — **rejected by the backend** |
| Position model enum | `'Fixed' \| 'Percentage'` — matches backend `PositionType` | `'Fixed' \| 'PercentOfEquity'` — **rejected by the backend** |
| AvoidOvernight | Exposed in `ExitSettingsForm` | Not settable, though the backtester supports it |

## Verified backend behavior (2026-08-06)

Verified empirically by deserializing test payloads against the real contract types
(net10.0, mirroring the API's `Program.cs` JSON options: `PropertyNameCaseInsensitive = true`):

1. **camelCase requests bind fine** (`Program.cs:93`). The Bruno collection already sends
   mixed casing successfully.
2. **Invalid enum values are rejected at the door.** `PositionType` and `ExitValueType`
   use strict `JsonStringEnumConverter` — `"PercentOfEquity"` or `"value"` throws
   `JsonException` → HTTP 400. The persisted Dynamo record stores the *parsed* request
   object (`BacktestHandler.Create` → `BacktestContextRecord.Request`), so **no stored
   backtest or strategy can contain the bad enum values** — every UI attempt to use
   dollar-based exits or PercentOfEquity sizing has simply failed. No legacy-data
   normalizer is needed anywhere.
3. **`required` members demand key presence, but accept explicit `null`.**
   `Cooldown`, `StopLoss`, `TakeProfit`, and `TimedExit` are all `required` (added
   2026-07-06). Omitting the key → 400. `"cooldown": null` → binds fine.
   - **Live bug in the current backtest page**: it omits disabled sections, so a
     default-config backtest (cooldown off) or any run with stop loss / take profit /
     timed exit disabled gets a 400 ("Backtest failed" toast).
   - **Latent strategy-editor bug too**: it submits `exitSettings: {}`, so creating a
     strategy with no exits configured also 400s (same shared models).
4. Worker null-handling today: `CheckStopLoss` / `CheckTakeProfit` and
   `Timeframe.ToTimeSpan()` (cooldown) are null-safe; `WorkerFunction.cs:268`
   dereferences `request.ExitSettings.TimedExit.Timeframe` unguarded.
5. Record round-trip: `BacktestRepository.cs:259/312` serializes/deserializes
   `record.Request` with default options; the Lambda payload uses `WhenWritingNull` but
   `OrchestratorRequest`/`WorkerRequest` have no `required` members, so absent members
   bind as null there.

## Decisions (locked 2026-08-06 — do not re-litigate)

- Strategy-side frontend code is the source of truth. Delete the PascalCase request
  shapes in `types/backtest.ts` entirely; no mapper layer.
- **Contract semantics (Rob): cooldown is optional; stopLoss, takeProfit, and timedExit
  are mandatory** — for both strategies and backtests, enforced consistently in frontend,
  API, and workers. A strategy/backtest without all three exits is invalid.
- **No filter enable/disable toggle** — use `EntrySettingsForm` as-is; further filter-UX
  design changes may come later.
- **Layout: single main column + sticky rail**, matching `StrategyEditorPage`.
- **Timespan options: drop month/year** — the shared forms' existing lists
  (minute/hour/day/week for cooldown, minute/hour/day for timed exit) stand.
- No PercentOfEquity/`'value'` data exists to migrate (verified above).

## Steps

### 1. Backend: make cooldown optional, enforce mandatory exits

Contracts (`packages/marketviewer-contracts`):

- `StrategyPositionSettings.Cooldown`: drop `required`, make nullable —
  `public Timeframe? Cooldown { get; init; }`. Consumers are already null-safe
  (`ToTimeSpan()` returns `Zero`; live cooldown path shares the same extension).
- `StrategyExitSettings`: keep `required` on `StopLoss`, `TakeProfit`, `TimedExit`.
  `required` only enforces key *presence* (explicit `null` still binds), so add explicit
  validation in the handlers — `BacktestHandler.Create` and the strategy create/update
  handlers return 400 with a clear message when `StopLoss`, `TakeProfit`, `TimedExit`,
  or `TimedExit.Timeframe` is null. Follow whatever validation pattern those handlers
  already use.
- Workers: no behavior change needed — `WorkerFunction.cs:268` is now guaranteed a
  non-null `TimedExit` by the API contract; keep the defensive null-checks in
  `CheckStopLoss`/`CheckTakeProfit` as-is.
- **Old-record compatibility check (do before shipping):** records created before
  `required` was added (2026-07-06) may lack `Cooldown` or exit keys in their stored
  `Request` JSON. Making `Cooldown` nullable fixes cooldown-less read-back; confirm
  whether any old backtest records / strategy records are missing `stopLoss` /
  `takeProfit` / `timedExit` (their read-back already fails today if so —
  `BacktestRepository.cs:312` deserializes with `required` enforcement). If any exist,
  decide: backfill defaults or tolerate at read (e.g. deserialize records with a
  lenient options instance).
- Deployment: contracts are compiled into each service — redeploy API, backtester
  lambdas, and Optimus together. The `.sln` build is broken (missing backtester-api
  project); build per-project.

### 2. Frontend types: redefine `BacktestRequest` in camelCase

In `apps/web/src/types/backtest.ts`:

- Delete `BacktestPositionSettings`, `BacktestModelSettings`, `BacktestCooldownSettings`,
  `BacktestExitTarget`, `BacktestTimedExitSettings`, `BacktestEntrySettings`,
  `BacktestExitSettings`.
- Redefine, importing from `./strategy`:

```ts
import type { PositionSettings, ExitSettings, EntrySettings } from './strategy';

export interface BacktestRequest {
  start: string;
  end: string;
  positionSettings: PositionSettings;
  entrySettings: EntrySettings;
  exitSettings: ExitSettings;
}
```

- In `types/strategy.ts`, reflect the new contract: `ExitSettings.stopLoss`,
  `takeProfit`, and `timedExit` become non-optional; `PositionSettings.cooldown` stays
  optional. (`TimedExit.timeframe` also becomes non-optional to match the backend.)
- Leave the read-model types (`BacktestEntry`, `BacktestRequestInfo`, stats types) alone —
  already camelCase and matching API responses. Cooldown: omit the key when disabled
  (JSON.stringify drops `undefined`) — the backend accepts absence once step 1 lands.

### 3. Shared `ExitSettingsForm`: exits are always configured

- Remove the enable/disable switches for stop loss, take profit, and timed exit — the
  three sections are always present with sensible defaults (SL −5%, TP +10%, timed exit
  30 min, matching current form defaults). `avoidOvernight` stays a toggle inside timed
  exit.
- `StrategyEditorPage.defaultFormData` gets fully-populated `exitSettings` instead of
  `{}` (fixes the latent no-exit-strategy 400 by construction).
- `PositionSettingsForm` needs no change (cooldown toggle already produces
  `cooldown: undefined` when off).
- Rail formatting (`formatExit`'s "Not set" branch) becomes dead for exits — simplify.

### 4. Rebuild `BacktestCreatePage` as a composition

Replace the ~20 `useState` hooks with `startDate`, `endDate`, and one `formData` object
(pattern-match `StrategyEditorPage`'s `update(patch)` helper).

- **Layout: single main column + sticky config rail**, like the strategy editor —
  masthead (title, date-range inputs, "← Results" + "Start Backtest" buttons, keep
  Clock / MarketStatus / ApiStatus), then Cards: 01 Entry, 02 Exit, 03 Position, with the
  rail summarizing filters / position / exits (reuse `RailRow`; add a date-range row).
- Keep the validation toasts (dates present, start ≤ end, ≥1 filter). Exits need no
  validation — the form can no longer express an invalid state.
- Submit payload is `{ start, end, ...formData }` verbatim — no mapping, no null helper.
- Prefill (`location.state.backtestDefaults`): keep the signature-based one-shot
  hydration; merge into `formData`, filling any missing exits with the defaults
  (relevant when copying old records). The per-field `hydrateFromRequest` mapping dies.
- The page gains `avoidOvernight` and dollar-based exits (`flat`) working for the first
  time, plus `Percentage` position sizing.

### 5. Fix the copy-backtest handoff (`BacktestDetailPage`)

`getCopyData()` (BacktestDetailPage.tsx:348) currently reconstructs a PascalCase request
from the camelCase read model. Rewrite it to emit the new camelCase `BacktestRequest`,
sharing the settings mapping with the existing `mapBacktestToStrategy` (lines ~310–345)
instead of duplicating it. No enum normalization needed (verified — bad values were never
storable). Old records missing exits hydrate to form defaults (step 4).

### 6. Delete dead code

Verified to have no importers:

- `apps/web/src/services/backtest.ts` — shadowed by a local function in `BacktestDetailPage`.
- `apps/web/src/config/defaults.ts` — `defaultBacktestRequest` has no consumers.
- `apps/web/src/components/forms/BacktestForm.tsx` — older-generation form; no importers.
- `apps/web/src/components/forms/StrategyForm.tsx` + `apps/web/src/components/modals/StrategyModal.tsx` —
  only import each other; nothing renders `StrategyModal`.

Re-verify each with a grep at implementation time before deleting.

### 7. Update the API collection

`api-collections/Api/Backtest/Create.yml`:

- camelCase properties, and fix the exit objects: they currently use the property name
  `"ExitValueType"` which does not bind to `Exit.Type` — it silently works only because
  `percent` is the enum's default value. Should be `"type": "percent"` (or `"flat"`).
- Show cooldown as optional (present in one example, absent in another or commented).

## Verification

1. Backend: contracts + API + backtester + Optimus build per-project; existing backend
   tests pass.
2. `tsc`/build for `apps/web` passes with the PascalCase types gone (compiler finds any
   missed consumer).
3. Create a backtest with **cooldown off** — previously a 400, must now succeed with the
   `cooldown` key absent from the payload.
4. API rejects a hand-crafted request (Bruno) with `"stopLoss": null` → 400 with the
   new validation message.
5. Dollar-based exits (`flat`) and `Percentage` position sizing produce sensible results —
   these paths have never worked from the UI before.
6. Copy an existing backtest (including one predating 2026-07-06 if available) → create
   page prefills correctly, missing exits filled with defaults; re-run succeeds.
7. Promote a backtest to a strategy; create/edit/clone a strategy — editor always shows
   the three exit sections populated.
8. Backtest with `avoidOvernight` on — confirm exits before close in results.
9. List/detail views still render old backtest records (old-record compatibility check
   from step 1).

## Out of scope / future

- Filter UX redesign (reordering/toggles) — deferred pending Rob's design changes.
