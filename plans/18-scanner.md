# Plan 18 — Scanner: saved scanners on the shared filter/create stack

## Context

The scanner is part of the core offer (LandingPage sells "Stock scanner (early access)"),
but "creating a scanner" does not exist anywhere today:

- **Frontend is a dead-end stub.** `apps/web/src/pages/ScannerPage.tsx` (39 lines) submits
  to `console.log`, has no results section, and — worse — is built on the *legacy*
  operand-tree filter model (`ScanArgument`/`Filter`/`Operand`, `types/strategy.ts:100+`
  under a "Legacy Types" header) rendered by `ArgumentConfigForm`/`FilterForm`/
  `TimeFrameForm`/`operands/*`. The backend counterpart of that model is 100% commented
  out (`ScanArgumentMapper.cs`, `ToolsScanHandler.cs`), so the page would produce a
  payload the backend cannot accept even if wired up.
- **The backend engine is real and running.** `ScanHandler`
  (`packages/marketviewer-application/.../Handlers/Market/Scan/ScanHandler.cs`) evaluates
  DSL filter expressions against the warm `IMarketCache`, cost-ordered via
  `ExpressionPlanner`. A stateless `POST /scan` endpoint
  (`apps/api/.../Controllers/Market/ScanController.cs` — class misnamed `TickerController`)
  takes `{ timestamp?, filters: string[], completedBarsOnly }` and returns matching
  tickers (`{ ticker, price, volume, float }`, capped at 1000). Separately, `ScannerJob`
  runs every 15s scanning the entry filters of every *active strategy* and publishing
  trade signals to SQS — that pipeline is strategy-driven and is **not touched** by this
  plan.
- **No scanner entity, persistence, CRUD, or `scannerApi.ts` exists.** The only durable
  scan artifact is a 5-minute-TTL result blob keyed by strategy hash (audit trail).

Meanwhile plans 12/13/15 built exactly the stack a scanner-create flow needs, already
shared by the strategy editor and backtest create page:

- `components/filters/FilterComposer.tsx` — smart text input: autocomplete from
  `GET /filters/functions`, debounced parser-backed validation via
  `POST /filters/validate`, signature hints, English echo, templates/recents, add gated
  on a valid parse. Takes `context: 'scan' | 'backtest' | 'chart'`.
- `components/filters/FilterChips.tsx` — AST-driven chips with click-to-edit;
  `filterExpression.ts` (serialize, templates, localStorage library).
- `components/forms/strategy/EntrySettingsForm.tsx` — the whole numbered filter-list UI
  (`{ value: EntrySettings; onChange }`), hosting composer + chips + reorder/delete.
- The create-page layout convention: masthead + `grid lg:grid-cols-[minmax(0,1fr)_320px]`
  main column + sticky rail (`RailRow`), `SectionHeading` "01/02/03" cards — see
  `StrategyEditorPage.tsx` and `BacktestCreatePage.tsx`.
- Backend conventions: handler-per-operation in `MarketViewer.Application`,
  `OperationResult<T>` + `HttpStatusCode`-switch controllers, FluentValidation
  validators, DynamoDB repositories in `marketviewer-infrastructure`.

A scanner is **entry filters only** — no exits, position sizing, or dates. So "identical
to backtest/strategy creation" means: same page layout, same `EntrySettingsForm` card,
same filter authoring, plus a name and a results table.

## Decisions (locked 2026-08-26, Rob — do not re-litigate)

- **Saved scanners with full CRUD** — named, per-user, persisted like strategies; list
  page + editor. Not just an ad-hoc scan page.
- **On-demand runs** via the existing `POST /scan`. Zero changes to
  `ScannerJob`/`ScannerCache`/`SignalPublisher`. Continuous server-side scanning and
  alerts are a future phase.
- **No filter-set entity.** A scanner's filters are the same `string[]` /
  `StrategyEntrySettings` shape strategies and backtests use. The "filter set" *is* that
  list, edited with the same `EntrySettingsForm`.
- **All three cross-flows in v1**: Scanner → Backtest, Scanner → Strategy, and
  Backtest/Strategy → "Save as scanner" (prefill handoffs via router state).
- **Tier: Pro** for scanner CRUD *and* running. `POST /scan` moves from
  `RequiresTier(Free)` to `Pro` in the same change (it has no frontend consumers today;
  Bruno only).
- **Server-side expression validation on create/update for all three** — scanner,
  strategy, and backtest. Closes the existing silent-failure hole (today no create
  endpoint parses expressions; a typo'd filter saved via raw API fails silently at
  scan/backtest time).
- **Results UX**: explicit Run button + an auto-refresh toggle (~30s) that only polls
  while the page is open and the market session is active.

## Steps

### 1. Contracts (`packages/marketviewer-contracts`)

- `Dtos/ScannerDto.cs` — `{ Id, UserId, Name, StrategyEntrySettings EntrySettings }`
  (reuse `StrategyEntrySettings` verbatim; do not invent a parallel filters model).
- `Requests/Management/Scanner/ScannerCreateRequest.cs` — `{ Name, EntrySettings }`;
  `ScannerUpdateRequest.cs` — same shape (id from route).
- `Responses/Management/ScannerResponse.cs` — mirrors the DTO (follow
  `StrategyResponse`).
- Naming: the entity is **Scanner**; avoid collisions with existing `ScanRecord`
  (audit blob), `ScannerCache` (live-loop strategy cache), and the backtester's
  `ScannerService` (historical replay) — none of which this plan touches.

### 2. Storage: reuse the strategy-store table

No new table / terraform. Scanners live in the existing `strategy` Dynamo table
(`infra/tf/app/dynamodb.tf:44`) under a new key prefix:

- `PK = SCANNER#{id}`, `SK = CONFIG`, plus top-level `UserId` attribute so the existing
  `UserIndex` GSI (hash `UserId`, projection ALL) serves "list my scanners".
- New `IScannerRepository` in `packages/marketviewer-core` + `ScannerRepository` in
  `packages/marketviewer-infrastructure` (pattern-match `StrategyRepository`:
  Create/Get/GetByUser/Update/Delete; no state/balance/active-strategies machinery).
- **Required guard:** `StrategyRepository` queries `UserIndex` by `UserId` alone
  (`StrategyRepository.cs:77`) and maps ids with `item["PK"].S.Split("BOT#")[1]`
  (`:351`) — scanner rows would pollute and break it. Add a
  `FilterExpression: begins_with(PK, :prefix)` (`BOT#`) to `GetByUser` and the
  `VisibilityIndex` query; the scanner repo's user query filters on `SCANNER#`
  symmetrically.

### 3. Server-side expression validation (scanner + strategy + backtest)

- Extract the parse + context-check core of `FilterValidateHandler`
  (`ParseExpression` try/catch + `FindContextViolation`) into a small reusable service,
  e.g. `MarketViewer.Application/Services/FilterExpressionValidator.cs`, consumed by
  `FilterValidateHandler` (behavior unchanged) and by the create/update paths.
- Wire it into `StrategyEntrySettingsValidator` via an injected custom rule — FluentValidation
  validators are DI-registered, so constructor injection works. Each invalid expression
  yields a per-filter error message (the parser's message, same text the composer shows).
- Contexts: scanner and strategy validate with `FilterContext.Scan`; backtest with
  `FilterContext.Backtest`. `StrategyEntrySettingsValidator` is shared by
  strategy/backtest today — parameterize the context (two validator registrations or a
  context argument) rather than forking the file.
- New `ScannerCreateRequestValidator` / `ScannerUpdateRequestValidator`: `Name`
  NotEmpty (whitespace-only rejected) and at most 100 characters; handlers persist the
  trimmed name. `EntrySettings` via the entry-settings validator.

### 4. API: `ScannerController` + scan endpoint housekeeping

- New `apps/api/MarketViewer.Api/Controllers/Management/ScannerController.cs`
  (`[ApiController] [Authorize] [Route("/scanner")]`, follow `StrategyController`):
  - `POST /scanner` → `ScannerCreateHandler`
  - `GET /scanner` → `ScannerListHandler` (current user's, via auth context UserId)
  - `GET /scanner/{id}`, `PUT /scanner/{id}`, `DELETE /scanner/{id}` — ownership checked
    in handlers (userId mismatch → 404, matching strategy handlers' pattern).
  - All actions `[RequiresTier(UserRole.Pro)]`.
  - Handlers in `packages/marketviewer-application/.../Handlers/Management/Scanner/`.
    **Unlike `StrategyCreateHandler`, do NOT touch `ScannerCache`** — saved scanners do
    not join the live signal loop.
- `ScanController.cs`: rename the class from `TickerController` to `ScanController`
  (route unchanged; also resolves the collision with the real `TickersController`), and
  change `[RequiresTier(UserRole.Free)]` → `Pro`.
- Note: the frontend calls `/api/scan` etc.; the rewrite rule in `Program.cs` strips the
  `api/` prefix — nothing to do, just don't add `api/` to the new route.

### 5. Frontend: types + API client

- `apps/web/src/types/scanner.ts` — `Scanner { id, name, entrySettings }` importing
  `EntrySettings` from `./strategy`; `ScanResult { ticker, price, volume, float }`,
  `ScanResponse { items, timeElapsed }`.
- `apps/web/src/api/scannerApi.ts` (pattern-match `strategyApi.ts`): CRUD calls + a
  `runScan(filters: string[], completedBarsOnly?)` wrapper for `POST /scan` (camelCase
  body binds fine — `PropertyNameCaseInsensitive` server-side).

### 6. Scanner list page

Replace `ScannerPage.tsx` wholesale (route `/scanner` stays; sidebar/topnav/home-card
entries already point here):

- react-query list of the user's scanners: name, filter chips (read-only
  `FilterChips`, first 2-3 + "+N"), edit/duplicate/delete actions, "New Scanner" button
  → `/scanner/new`. Pattern-match the strategy dashboard's list/delete mutation flow
  (`pages/optimus/DashboardPage.tsx`).
- Empty state points at "New Scanner" and the filter docs (`/docs/filters`).

### 7. Scanner editor + run page (the core deliverable)

New `apps/web/src/pages/ScannerEditorPage.tsx`, routes `/scanner/new` and
`/scanner/:scannerId` (edit and run are one page — a scanner's "detail view" is
running it):

- **Same skeleton as `StrategyEditorPage`/`BacktestCreatePage`**: masthead (name input,
  "← Scanners", Save / Run Scan buttons, Clock / MarketStatus / ApiStatus), main column
  + sticky rail. One `formData` + `update(patch)` state object; save via react-query
  mutation (create vs update by presence of `scannerId`).
- **Card 01 Entry Filters = `EntrySettingsForm` as-is** (`{ value, onChange }`).
- **Results card**: runs `scannerApi.runScan(filters, completedBarsOnly)` on
  Run — sortable table (Ticker · Price · Volume · Float), match count + `timeElapsed`,
  a "results are capped at 1000" note when the cap is hit (the response carries no
  truncation flag, so the wording stays correct at exactly 1000 matches).
  Auto-refresh toggle: 30s interval, active only while mounted; pause and surface
  "market closed" when the session is inactive (reuse whatever `MarketStatus` already
  consumes). A `CompletedBarsOnly` toggle (default off) exposed as a small "completed
  bars only" option next to Run.
  Running does not require saving first (filters go straight to `/scan`), but prompt
  unsaved-changes on nav away.
- **Rail**: filter summary (`FilterChips` read-only, like the other two pages), match
  count, last-run time.
- Extract the `SectionHeading` component duplicated verbatim in
  `StrategyEditorPage.tsx:119` and `BacktestCreatePage.tsx:74` into
  `components/forms/SectionHeading.tsx` and use it in all three pages (don't
  triplicate).
- Thread a `context` prop through `EntrySettingsForm` → `FilterComposer` (optional,
  default `'scan'`): scanner and strategy pass `'scan'`, `BacktestCreatePage` passes
  `'backtest'` — fixing the existing hardcode at `EntrySettingsForm.tsx:161` that
  validates backtest filters against the scan context.

### 8. Cross-flows (prefill handoffs)

All via router state, matching existing conventions:

- **Scanner → Backtest**: "Backtest these filters" button →
  `navigate('/backtest/create', { state: { backtestDefaults } })` with
  `entrySettings` from the scanner and exits/position filled from the shared defaults
  (`defaultExitSettings` is already exported from `ExitSettingsForm`; export the
  position defaults from `BacktestCreatePage`'s `defaultFormData` or a shared module
  rather than duplicating). `BacktestCreatePage`'s existing one-shot hydration
  (`:89-117`) handles the rest.
- **Scanner → Strategy**: "Promote to strategy" →
  `navigate('/optimus/strategy/new', { state: { initialData: { name, entrySettings } } })` —
  `mergeIntoDefaults` already tolerates partial data (`StrategyEditorPage.tsx:168-173`).
- **Backtest/Strategy → Scanner**: "Save as scanner" action in the `StrategyEditorPage`
  rail and on `BacktestDetailPage` (next to its existing copy/promote actions) →
  `navigate('/scanner/new', { state: { initialData: { name?, entrySettings } } })`;
  the scanner editor consumes `initialData` the same one-shot way.

### 9. Delete the legacy scanner filter model

With `ScannerPage` rebuilt, the legacy authoring tree loses its last consumer. Verify
each with a grep at implementation time, then delete:

- `components/forms/strategy/ArgumentConfigForm.tsx`, `FilterForm.tsx`,
  `TimeFrameForm.tsx`, `operands/OperandForm.tsx`, `StudyOperandForm.tsx`,
  `PriceActionOperandForm.tsx`, `FixedOperandForm.tsx`.
- The legacy block in `types/strategy.ts` (`Filter`, `Operand`, `ScanArgument`, …) —
  **except** what `components/backtest/FilterDisplay.tsx` still needs for its read-only
  fallback rendering of old persisted backtests; move those types next to
  `FilterDisplay` if that's all that remains.
- Dead leftovers in `types/filters.ts:1-17` (`FilterFunctionName`, `DraftArg`).
- Backend dead code (all already non-functional; delete, don't comment further):
  `ScanArgumentMapper.cs` (fully commented), `FilterDto.cs`, `OperandDto.cs`,
  `Enums/Scan/{FilterType,FilterOperator,FilterTypeModifier,FilterValueType,OperandModifier}.cs`,
  commented-out `ToolsScanHandler.cs` + the commented `ScanArgumentMapperUnitTests.cs`.

### 10. Pro gating in the UI

- `userApi` already exposes `role: 'Free' | 'Pro' | 'Premium'`. On `/scanner` routes,
  Free users see the page shell with an upgrade panel linking to `/billing` instead of
  the list/editor (server 403 via `RequiresTier` is the backstop, not the UX).
- Update `LandingPage`/`HomePage` scanner copy if "early access" no longer fits.

### 11. API collection

New `api-collections/Api/Scanner/{Create,List,Read,Update,Delete}.yml` (camelCase
bodies), and update `Api/Market/Scan.yml` to note the Pro tier.

## Files touched (expected)

| Area | Files |
|---|---|
| Contracts | new `Dtos/ScannerDto.cs`, `Requests/Management/Scanner/*`, `Responses/Management/ScannerResponse.cs` |
| Core / Infra | new `IScannerRepository.cs`, `ScannerRepository.cs`; `StrategyRepository.cs` (PK-prefix filters) |
| Application | new `Handlers/Management/Scanner/*` (Create/List/Read/Update/Delete), `Services/FilterExpressionValidator.cs`, `Validators/Scanner*Validator.cs`; `Validators/StrategyEntrySettingsValidator.cs` (context-aware parse rule); `FilterValidateHandler.cs` (extract core) |
| API | new `Controllers/Management/ScannerController.cs`; `Controllers/Market/ScanController.cs` (class rename + Pro tier); DI registrations |
| Web | `pages/ScannerPage.tsx` (rewrite as list), new `pages/ScannerEditorPage.tsx`, new `api/scannerApi.ts`, new `types/scanner.ts`, `routes/index.tsx`, new `components/forms/SectionHeading.tsx`, `EntrySettingsForm.tsx` (context prop), `StrategyEditorPage.tsx` / `BacktestCreatePage.tsx` (SectionHeading swap, context prop, Save-as-scanner), `BacktestDetailPage.tsx` (Save as scanner), deletions per step 9 |
| Collections | new `Api/Scanner/*.yml`; `Api/Market/Scan.yml` |
| Infra | none (table reuse) |

## Verification

1. Backend builds per-project (contracts, core, infrastructure, application, api — the
   `.slnx` covers it; don't create a `.sln`); existing suites stay green.
2. Unit tests: scanner CRUD handlers (create/list/read/update/delete + ownership 404),
   validators (empty name, empty filters, typo'd expression → per-filter parser message,
   context violation — a backtest-only function rejected for a scanner), and
   `FilterValidateHandler` regression (behavior unchanged after the extraction).
3. Strategy/backtest create with a typo'd filter (Bruno, bypassing the UI) now 400s
   with the parser message — previously accepted silently.
4. Strategy list (`GET /strategy`) returns no scanner rows after creating scanners
   (PK-prefix filter works); scanner list returns only the caller's scanners.
5. `tsc`/build for `apps/web` passes with the legacy components/types deleted.
6. Full flow: create scanner → shows in list → open → Run returns tickers →
   auto-refresh ticks while market open, pauses closed → edit filters (chips
   click-to-edit + composer) → save → reload persists.
7. Cross-flows: scanner → backtest create prefilled (filters intact, defaults filled);
   scanner → strategy editor prefilled; backtest detail and strategy editor →
   "Save as scanner" lands in the scanner editor with filters prefilled.
8. Tier: Free user gets 403 from `/scanner` CRUD and `/scan`, and sees the upgrade
   panel instead of the editor; Pro user unaffected.
9. Old backtest records with operand-shaped filters still render on
   `BacktestDetailPage` (`FilterDisplay` fallback survives the type cleanup).
10. Live pipeline untouched: `ScannerJob`/`PopulateScannerJob`/`SignalPublisher` —
    no diff; creating a scanner does not enqueue SQS messages or enter `ScannerCache`.

## Out of scope / future

- **Continuous server-side scanning + alerts** — saved scanners joining the 15s loop
  with a discriminator so matches don't publish trade signals, persisted results, and
  notifications. The on-demand v1 deliberately avoids touching that pipeline.
- **Named reusable filter-set entity** shared across scanners/strategies/backtests —
  revisit if per-user filter reuse outgrows the localStorage library + cross-flow
  prefills.
- Scan result history / persistence beyond the in-page table.
- Scanner sharing/visibility (strategies have `VisibilityType`; scanners start private).
- Free-tier limited quota (chosen: hard Pro gate; revisit if funnel data argues for a
  taste of the scanner on Free).
- Extra result columns (change %, RVOL, …) — needs `ScanResponse` enrichment; keep the
  v1 payload as-is.
