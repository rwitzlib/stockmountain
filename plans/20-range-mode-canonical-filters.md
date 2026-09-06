# Plan 20 — Range mode (`any`/`all`), strict bracket suffix, server-side canonical filters

Grilled 2026-09-05. Trigger: `rsi(14,70,30,wilders) > 70 [1m, 5, any]` lost its `any` when added
through the backtest create form. Investigation showed the DSL already supports the mode; the loss is
a lossy UI round-trip. The fix grew into a small redesign of how filter text flows between the parser
and the UI, because more DSL additions are coming soon and every one of them would hit the same trap.

## What is true today

- Parser (`ExpressionParser.ParseTimeframeAndRange`) accepts `[timeframe, candles, mode]` parts in
  **any order**, each optional. `RangeEvaluationMode` defaults to `All`. Every comparison operator
  honors the mode via `RangeEvaluationHelper.Evaluate`. `docs/filters/index.md` documents this
  correctly. `packages/marketviewer-filters/.../README.md` line ~448 is stale (claims ANY default).
- `FilterValidateHandler.MapNode` projects `TimeframeRangeExpression` to a presentation
  `FilterAstNode` with `Timeframe` + `Candles` only — **no mode**. The TS mirror
  (`apps/web/src/types/filters.ts`) has no mode either, and `serializeAst` in
  `components/filters/filterExpression.ts` re-emits `[tf, N]`. Any chip edit therefore rewrites
  `any` → default `all` silently. `DescribeRange` says "over the last N candles" for both modes, so
  nothing on screen reveals the change.
- `crosses_over` / `crosses_under` ignore mode (`CrossDetector` reads `CandleRange` only). `[tf, R]`
  on a cross already means "any cross in the last R bars".
- Short window: comparisons check `min(N, available)` bars; `all` over 2 bars passes when 5 were
  asked for. Python golden ref (`tools/golden/compute_outcomes.py`) models the same rule.
- Default timeframe for a bare line: backtester and scan handler use 1m; the chart filter tool
  (`ToolsFilterHandler`) passes the chart's timeframe **and evaluates every filter against chart
  bars regardless of the bracket** — bracket timeframes are effectively ignored on that page.
- The composer's signature hint (`enclosingFunction`) only fires inside `(`; nothing for `[`.
- The web app has **no test runner** (no vitest/jest, zero test files). `serializeAst` promises
  round-trip fidelity with nothing enforcing it.

## Locked decisions

1. **Strict bracket order in the parser: `[timeframe, candles, mode]`.** No stored strategy uses a
   nonstandard order (confirmed by Rob 2026-09-05); no migration.
2. **Inside a bracket, timeframe is required.** These are validation errors, never silently
   reinterpreted:
   - `[5]` (bare candles)
   - `[, 5]` (leading empty slot)
   - `[1m, any]`, `[1m, all]`, `[1m, 1, any]` (mode without candles > 1)
   - `[any]`, `[all]`
   - any bracket at all on a **scalar-only** line (`float > 1000000 [1m]`) — registry `IsScalar`
     already marks `float`
   - explicit `all` on a **cross-only** line (crosses are inherently any-of-range)

   Mixed lines (`close > sma(20) AND crosses_over(close, vwap()) [1m, 5, all]`) are accepted; the
   description spells out that `all` governs the comparison and the cross is any-of-range.
3. **Canonical printer rules.** Series lines always carry an explicit timeframe (`close > sma(20)`
   → `close > sma(20) [1m]`). Mode is always emitted when candles > 1 (`[1m, 5]` → `[1m, 5, all]`).
   Cross-only lines print `any`. Scalar-only lines stay bare, so a bare line reliably signals a
   scalar filter. Existing parenthesization rule (wrap a logical operand of a different operator)
   is kept.
4. **`all` requires the full window.** Fewer than N candles available → false. `any` unchanged.
   This shifts some historical backtest numbers at window edges for every existing `[tf, N]` filter
   with N > 1; accepted. Python golden ref changes to match; golden case `range-without-tf`
   (`[, 2]`) is removed and negative parser cases added.
5. **Canonicalization is server-side; the TS serializer is deleted.** `/filters/validate` returns
   the canonical expression plus an AST whose nodes carry **character spans** into that canonical
   string. A chip edit is a text splice on the span followed by re-validate; the client never
   mutates or serializes an AST. Presentation AST shrinks to kind / role / span / editable-kind.
   Parser must track token positions; printer must be deterministic so spans are stable.
6. **All three contexts default to 1m.** The chart filter tool's default moves to 1m in this
   change. Longer term the chart page inverts: filters pick the chart(s) — one chart per distinct
   timeframe with synced highlighting — so a bare line never has a chart to defer to.
7. **Composer bracket hint** mirrors the function hint: slot highlighting for
   timeframe / candles / mode, with `any`/`all` and timeframe tokens offered in autocomplete inside a
   bracket. Catalog-driven: `/filters/functions` gains a pseudo-entry for the suffix.

## Assumptions stated during the grill (not objected to)

- Strategy/backtest saves store the canonical text (validator already runs on save). Existing
  records are rewritten on their next save, not migrated.
- Canonical text replaces the composer input on commit (add filter / apply chip edit), not on
  keystroke.
- No pre-deploy scan of stored filters against the strict parser. The other new rejections
  (`[, N]`, bare `[N]`, bracket on float-only lines, `all` on cross-only lines) are an accepted risk
  on Rob's confirmation. A stored filter that fails the strict parser will fail loudly at runtime
  in the backtester / live scanner.
- Chip mode toggle is optional; the bracket hint covers discoverability. Do the round-trip fix first.

## Work breakdown

**Parser / engine (`MarketViewer.Filters`)**
- Strict positional `ParseTimeframeAndRange` with the error messages above; token spans on every
  expression node; scalar-only and cross-only line detection for the bracket rules.
- Canonical printer (expression tree → text) with the rules in decision 3.
- `RangeEvaluationHelper` / operators: `all` returns false when `count < range`.
- README stale-default fix; `docs/filters/index.md` suffix table updated (strict order, required
  timeframe, `all` full-window rule, scalar/cross rules); `crosses_over.md` / `crosses_under.md`
  note the `any` printing.

**Validate endpoint (`FilterValidateHandler`, contracts)**
- Response: `canonical` string, AST nodes with `span` (+ mode), description that names the mode
  ("on all of the last 5 1m candles" / "on any of").
- Functions catalog: bracket-suffix pseudo-entry.

**Chart tool** — default timeframe 1m.

**Web (`apps/web`)**
- Delete `serializeAst`; chips render from spans over canonical text; edits splice text and
  re-validate; composer bracket hint + in-bracket autocomplete; input replaced with canonical on
  commit.

**Tests**
- Parser unit tests for every rejection case and the strict order; printer round-trip
  (parse → print → parse yields identical tree, spans line up); operator tests for full-window
  `all`; golden ref update + parity run; validate-handler tests for spans/description.
