# Plan 13 — Filter builder UX: text-first input, parser-backed validation, chips as output

## Context

Entry filters are authored as DSL expressions (`rsi(14) < 30 [1m]`,
`macd(12,26,9,ema).histogram > 0 [5m,3]`, `crosses_over(close, sma(20))`) and evaluated
by `IndicatorExpressionEngine` in `packages/marketviewer-filters`. The frontend authoring
component (`apps/web/src/components/filters/FilterComposer.tsx`) has two modes, both weak:

- **Builder**: palette buttons that append string tokens with no grammar awareness —
  it can happily produce `close > > [1m]`, and it's slower than typing.
- **Code**: a bare `<Input>` with no autocomplete, no signature help, and no validation.
  A typo (`rsl(14) < 30`) is accepted into the filter list and only fails later, inside
  a scan or backtest.

The frontend knows nothing about the grammar, but **a complete parser already exists in
C#**: `IndicatorExpressionEngine.ParseExpression(string) → IExpression`, with typed AST
nodes (`BinaryExpression`, `FunctionCallExpression`, `FieldAccessExpression`,
`LiteralExpression`, `TimeframeRangeExpression`, `DataAccessExpression`). The strategy
is to expose that parser to the UI rather than re-implement any grammar in TypeScript.

## Decisions (locked 2026-08-06 — do not re-litigate)

- **Text-first**: typing is the entry path. No drag-and-drop authoring — the grammar is
  short and linear, and nesting (`crosses_over`, dot-fields, future arithmetic from
  plan 11) doesn't survive chip-dragging. Drag stays only where it already works:
  reordering whole filters in the list.
- **Chips are output, not input**: committed filters render as structured, clickable
  segments derived from the *parse tree*, never from client-side string splitting.
- **The C# parser is the single source of grammar truth**, surfaced via a validate
  endpoint. No TypeScript parser.
- **The Builder/Code mode split dies** (phase 2); one smart input replaces both.
- Phased delivery: 1) validation + smart input, 2) AST chips + click-to-edit,
  3) library/templates. Each phase ships independently.

## Phase 1 — Validate endpoint + smart input

### 1a. Backend: `POST /filters/validate`

New `FiltersController` in `apps/api/MarketViewer.Api/Controllers/Market/` (follow
`BacktestController` conventions: `[ApiController]`, `[RequiresTier(UserRole.Basic)]`,
handler in `MarketViewer.Application`).

Request: `{ "expression": "rsi(14) < 30 [1m]" }`

Response (200 always; validity is in the body):

```jsonc
{
  "valid": true,
  "error": null,            // parser message when invalid, e.g. "Unknown function: rsl"
  "description": "RSI(14) on the 1-minute chart is below 30",  // English echo, from AST
  "timeframe": { "multiplier": 1, "timespan": "minute" },      // via ExtractTimeframe
  "ast": { /* simplified tree, see below */ }
}
```

Implementation notes:

- Handler wraps `ParseExpression` in try/catch; parser throws
  `InvalidOperationException`/`ArgumentException` with message-only errors (no
  positions — see Out of scope). Return `valid: false` + the message.
- AST DTO: a small recursive shape mapped from the `IExpression` node types, e.g.
  `{ kind: "binary", op: ">", left: {...}, right: {...}, range: { timeframe, candles } }`,
  `{ kind: "function", name: "rsi", args: ["14"], field: "value" }`,
  `{ kind: "literal", value: "close" }`. Keep it presentation-oriented; it only needs
  enough for chip rendering and popover editing, not evaluation.
- `description` (English echo) is generated server-side from the AST so there is exactly
  one phrasing implementation. Simple recursive formatter; fall back to the raw
  expression for nodes it doesn't know.
- Also add `GET /filters/functions` returning autocomplete metadata (name, signature,
  parameter docs, insertion snippet). Static list in the handler is fine — sourced from
  the README table: `sma`, `ema`, `rsi`, `macd`, `adv`, `slope`, `crosses_over`,
  `crosses_under`, `support_resistance`/`sr`, plus literals `close/open/high/low/vwap/
  volume/float/time` and their `.field` completions. Serving it from the API (instead of
  hardcoding in the frontend) keeps one place to update when plan 11 adds functions.

### 1b. Frontend: smart input

Rework `FilterComposer` (keep the component name and `onAddFilter` contract so
`EntrySettingsForm` is untouched):

- Single text input, monospace, replacing the Code mode. Leave the palette Builder in
  place until phase 2 removes it.
- **Autocomplete** dropdown fed by `GET /filters/functions` (cached via react-query):
  triggers on word boundaries; `Enter`/`Tab` inserts. Function entries insert their
  snippet (`macd(12,26,9,ema)`) with the first argument selected; `Tab` cycles
  arguments (track selection ranges — plain input + `setSelectionRange` is enough, no
  editor library).
- **Signature hint** line while the caret is inside a function's parens.
- **Debounced validation** (~300 ms) against `POST /filters/validate`: ✓/✗ affordance,
  parser error message inline under the input, and the **English echo** line when valid.
- **Add is gated on a valid parse** — the add button submits only after a passing
  validate call (fire a final non-debounced validate on click). This closes today's
  silent-failure hole on its own.
- Offline/API-down behavior: if the validate call itself fails (network), allow add with
  a "not validated" warning rather than blocking authoring.

## Phase 2 — Chips as output

- Extend the validate response usage: when a filter is added (and when rendering
  existing filters), request/carry its AST and render **segmented chips** instead of a
  flat `<code>` string: left operand · operator · right operand · timeframe pill, each
  segment colored by role (function, literal, number, timeframe).
- **Click-to-edit segments**: clicking a segment opens a small popover editing just that
  piece — numeric field for function args and comparison values, dropdown for operators
  (`> < >= <= = !=`), dropdown for the timeframe pill (`1m/5m/15m/1h/1d` + candle
  count). Edits re-serialize from the AST and re-validate before committing, so chips
  can never produce an invalid expression.
- Chip renderer lives in one shared component (e.g.
  `components/filters/FilterChips.tsx`) used by: `EntrySettingsForm` (strategy editor +
  backtest create), the config rails, `FilterDisplay` on the backtest detail page, and
  `SharedBacktestPage`. Read-only surfaces render the same chips without edit affordance.
- Filters whose AST fetch fails (legacy/unparseable) fall back to the current plain
  `<code>` rendering.
- Delete the palette Builder mode and the `BuilderToken` machinery from
  `FilterComposer`; delete `FilterPaletteGroup` types if unused elsewhere.
- Batch consideration: `EntrySettingsForm` renders N filters — validate/AST calls should
  batch (`POST /filters/validate` accepts `expressions: string[]`) or be cached by
  expression string in react-query to avoid N requests per render.

## Phase 3 — Library and templates

- **Recent + pinned filters**: store per-user in `localStorage` v1
  (`{ expression, lastUsed, pinned }`, capped ~50, keyed by normalized expression).
  Surface as a searchable dropdown section in the smart input (empty-input state shows
  recents/pins; the current hardcoded quick-add examples in `EntrySettingsForm` fold
  into this as seed templates). A backend user-prefs store is a later upgrade if
  cross-device matters.
- **Template gallery**: a handful of named, complete starting points ("Oversold bounce"
  `rsi(14) < 30 [1m]`, "Volume surge" `volume > 1000000 [1d]`, "Above the 200-day"
  `close > sma(200) [1d]`, "Liquidity floor" `adv() > 2000000 [1d]`). Static frontend
  list; insert-and-modify workflow.

## Files touched (expected)

| Area | Files |
|---|---|
| API | new `Controllers/Market/FiltersController.cs`; new handler + response contracts in `MarketViewer.Application`/`MarketViewer.Contracts`; DI registration |
| Filters pkg | none required (parser used as-is); optional error-position work is out of scope |
| Web | `components/filters/FilterComposer.tsx` (rework), new `FilterChips.tsx` (phase 2), `FilterList.tsx`, `FilterDisplay.tsx`, `EntrySettingsForm.tsx` (render swap only), new `api/filtersApi.ts` |
| Collections | new `api-collections/Api/Filters/Validate.yml` |

## Verification

1. Validate endpoint: valid expressions from the README all return `valid: true` with
   sensible `description`; `rsl(14) < 30`, `close > > 5`, empty string return
   `valid: false` with the parser's message. Unit tests in
   `MarketViewer.Application.UnitTests` (happy path + each error class).
2. Smart input: typo cannot be added (button gated); autocomplete inserts snippets and
   tab-cycles args; echo line matches the expression's meaning.
3. Phase 2: chips round-trip — segment edit → re-serialize → re-validate → identical
   semantics; legacy/unparseable filter falls back to plain text without crashing.
4. Existing flows regress nothing: strategy create/edit, backtest create, detail pages,
   shared backtest page all render filters (chips or fallback).
5. `tsc` error set stays at or below the current baseline; backend suites stay green.

## Out of scope / future

- **Positioned parser errors** (squiggle underlines): `ExpressionParser` throws
  message-only exceptions today. Adding token positions is a self-contained parser
  enhancement that would upgrade inline errors — worth doing when touching the parser
  for plan 11 work, not blocking any phase here.
- Plan 11 DSL capabilities (arithmetic, rvol, trailing stops, etc.) — when they land,
  the `GET /filters/functions` list and English-echo formatter are the only two places
  this plan needs updating.
- Backend-persisted filter library (cross-device sync).
