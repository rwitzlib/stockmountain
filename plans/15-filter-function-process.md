# Plan 15 — Adding filter functions at scale: single-source registry, docs, and an agent skill

> **Status 2026-08-18 — ALL FOUR PHASES IMPLEMENTED (uncommitted).**
> Phase 1: `Registry/` (`FilterFunctionAttribute`, `FunctionDescriptor`, `FunctionRegistry`,
> `KeywordRegistry`); parser table, `FunctionHeuristicsRegistry`, `DataAccessExpression.IsScalar`,
> `/filters/functions` (+`?context=`, `contexts`, `docsUrl`, `aliases`, `functionKind`) and
> `/filters/validate` (`context` enforcement) all derived; `FunctionCatalog` deleted;
> `RegistryParityTests` (109 cases; probe with a dummy `[FilterFunction("zzz")]` fails exactly the
> golden/docs/incremental checks). Legacy exceptions recorded in the test: `support_resistance` is
> snapshot-only and non-incremental. Phase 2: 17 pages + `index.md` under `docs/filters/`,
> `/docs/filters[/:name]` route (`FilterDocsPage`, `content/filterDocs.ts`, `marked`), autocomplete
> "docs ↗" links; package README de-staled. Phase 3: `.claude/skills/add-filter-function/` (SKILL +
> 6 references), `.github/pull_request_template.md`, `.github/skills` pointer, `tools/golden/README`
> pointer. Phase 4: `Indicator.Type` is a string resolved via `FunctionRegistry` (Chart context) in
> `IndicatorCalculationService`; `StudyType` enum deleted; `IndicatorCalculationServiceUnitTests`.
> Not done: web chart `REGISTRY` kept as rendering config (panes/colours can't come from the API) —
> commented; legacy `StudyOperandForm` `<option>` list untouched (legacy structured-filter path).
> Open: `RequiredBars(args)` warm-up metadata (not implemented). Golden outcome cases added for
> `high`, `low`, `float` (keyword coverage). Decisions below are locked — do not re-litigate.

## Context

The filter DSL (`rsi(14) < 30 [5m,3]`, `close crosses over vwap()`, `float < 50000000`, …) will
grow from ~10 functions to potentially hundreds. Today, adding one function means hand-editing
**seven parallel registries** — the `vwap` commit (`e324a1f`) touched 26 files:

| Registry | File | What happens if forgotten |
|---|---|---|
| Parser function table | `packages/marketviewer-filters/MarketViewer.Filters/Parsing/ExpressionParser.cs:29–42` (`_functions`); bare keywords at `:487–494` | Function does not parse |
| Autocomplete catalog | `packages/marketviewer-application/.../Handlers/Market/Filters/FilterValidateHandler.cs:218–253` (`FunctionCatalog`) | UI never suggests it; signature hints missing |
| Cost/selectivity heuristics | `.../Expressions/FunctionHeuristicsRegistry.cs:7–28` | Silently defaults to `(2, 0.5)`; filter ordering suboptimal |
| Python golden reference | `tools/golden/compute_reference.py` + `compute_outcomes.py` + 7 regenerated fixture JSONs | No independent oracle; golden suite has no key for it |
| Web templates | `apps/web/src/components/filters/filterExpression.ts:111–118` (`FILTER_TEMPLATES`) | No example in the templates library |
| Package README | `packages/marketviewer-filters/MarketViewer.Filters/README.md` | Already stale (documents `vwap` as a bare literal) |
| Chart wiring (only if chartable) | `MarketViewer.Contracts/.../Enums/StudyType.cs`, `.../Services/IndicatorCalculationService.cs:68–77,125–177`, `apps/web/src/config/indicators.ts`, `apps/web/src/types/tools.ts:39` | Not available on `/stocks` charts |

There is **no user-facing documentation** anywhere; users learn the DSL from autocomplete
descriptions only. There is no process doc for agents beyond `README.md ## Contributing`
(stale) and `tools/golden/README.md` ("Adding a new indicator function"). `.claude/` has no
skills (the existing `grill-me` etc. live in `.github/skills/`, which Claude Code does not load).

Note on `/stocks`: it does **not** accept the DSL. It takes `Indicators` typed by the `StudyType`
enum and dispatches through `IndicatorCalculationService`. So "works with /stocks" is really
"is chartable" — a separate axis from "is a filter". `float` and `time` are filter-only; `sma`
is both.

## Decisions (locked 2026-08-18 — do not re-litigate)

1. **Consolidate first, then write the skill.** The skill describes the *target* process, not
   the seven-registry status quo.
2. **Single source of truth = an attribute on the function class.**
   `[FilterFunction(...)]` on each `IFunction` carries name, aliases, kind, signature, params,
   description, cost/selectivity, and allowed contexts. Parser table, `/filters/functions`
   catalog, and heuristics are all built by reflection over the `MarketViewer.Filters` assembly.
   Forgetting to register becomes impossible.
3. **Contexts are declared and enforced.** `Scan | Backtest | Chart` (bit flags). `/filters/validate`
   accepts an optional `context` and rejects functions not valid there. `/stocks` builds its
   indicator list from the same registry so `StudyType` can be retired (phase 4).
4. **Testing bar is strict, tiered by kind:**
   - *Series indicator* (`sma`, `rsi`, …): unit tests + Python golden reference key(s) +
     `Incremental_Matches_Full` (automatic via golden) + perf test.
   - *Boolean / transform / comparison* (`crosses_over`, `slope`): unit tests + golden outcome cases.
   - *Bare keyword / scalar* (`float`, `time`): unit tests + golden outcome cases.
   - Always: parity test coverage (see §Phase 1c).
5. **`IIncrementalSeriesFunction` is required** for every new series indicator (live scanner and
   backtest hot paths depend on it). Contract: last point is provisional and must be recomputed
   on `Append` (plan 14 follow-up #1).
6. **Python golden reference is required and the agent runs it.** Prefer a library
   implementation (`pandas_ta`/`ta-lib`) when one exists. For custom indicators, implement it in
   `compute_reference.py` in numpy/pandas **from the docs page spec, not by porting the C#** —
   a line-for-line port is a tautology and does not count. Regenerated fixture JSONs are
   committed and the diff is reviewed.
7. **User docs live in the web app** at `/docs/filters/:name`, rendered from repo markdown
   (`docs/filters/<name>.md`), bundled at build time, publicly accessible (signed-out), and
   linked from the autocomplete popover via a `docsUrl` on `/filters/functions`. Each page is the
   **spec** both the C# and Python implementations are written against — it must state formula,
   warm-up/seeding, forming-bar and timeframe behaviour, not just what the indicator "is".
8. **Enforcement = parity tests + skill self-check + PR template.** Unit tests fail if any
   registered function lacks catalog metadata, heuristics, a docs file, a golden key/case, or
   (series) `IIncrementalSeriesFunction`.
9. **Skill covers both "add new" and "modify existing".** The modify path adds: bump
   `ScannerService.CacheVersion`, clear the S3 entry cache, regenerate golden refs with an
   explained diff, and a changelog entry on the docs page. This is where the 2026-07 stale-cache
   and nondeterminism bugs came from.
10. **Skill lives at `.claude/skills/add-filter-function/`** (SKILL.md + reference files). A
    one-line pointer goes in `.github/skills/` for other agent tools.

## Phase 1 — Attribute registry + parity tests (`MarketViewer.Filters`, application, tests)

### 1a. Metadata model

New in `MarketViewer.Filters/Registry/`:

```csharp
[Flags] public enum FilterContext { None = 0, Scan = 1, Backtest = 2, Chart = 4, All = Scan|Backtest|Chart }
public enum FunctionKind { Series, Boolean, Transform, Keyword }

[AttributeUsage(AttributeTargets.Class)]
public sealed class FilterFunctionAttribute : Attribute
{
    public FilterFunctionAttribute(string name) { Name = name; }
    public string Name { get; }
    public string[] Aliases { get; init; } = [];          // e.g. "sr" for support_resistance
    public FunctionKind Kind { get; init; }
    public string Signature { get; init; } = "";          // "rsi(period)"
    public string Snippet { get; init; } = "";            // "rsi(14)"
    public string Description { get; init; } = "";        // one-liner for autocomplete
    public string[] Params { get; init; } = [];           // "period:int"
    public string[] Fields { get; init; } = [];           // ".signal", ".histogram"
    public int Cost { get; init; } = 2;                   // FunctionHeuristicsRegistry values
    public double Selectivity { get; init; } = 0.5;
    public FilterContext Contexts { get; init; } = FilterContext.Scan | FilterContext.Backtest;
}

public sealed record FunctionDescriptor(string Name, string[] Aliases, FunctionKind Kind, string Signature,
    string Snippet, string Description, string[] Params, string[] Fields, int Cost, double Selectivity,
    FilterContext Contexts, Type ImplementationType, bool IsIncremental);

public static class FunctionRegistry
{
    // Reflect once over the assembly; throws at startup on duplicate names/aliases.
    public static IReadOnlyList<FunctionDescriptor> All { get; }
    public static bool TryGet(string nameOrAlias, out FunctionDescriptor d);
    public static IFunction Create(FunctionDescriptor d);   // parameterless ctor or Activator
}
```

Bare keywords (`close/open/high/low/volume/float/time`) get the same treatment via a small
`[FilterKeyword("float", Kind=Keyword, Contexts=Scan|Backtest, IsScalar=true)]` on descriptor
classes (or a `KeywordDescriptor` list next to `DataAccessExpression`) so `IsDataAccessKeyword`,
`DataAccessExpression.IsScalar`, and the catalog all read one list.

### 1b. Derive the consumers

- `ExpressionParser._functions` → built from `FunctionRegistry.All` (parser stays otherwise unchanged).
- `FunctionHeuristicsRegistry` → reads `Cost/Selectivity` from the descriptor; delete the hand table.
  Keep the API shape (`ExpressionPlanner` untouched).
- `FilterValidateHandler.FunctionCatalog` → deleted; `/filters/functions` maps descriptors →
  `FilterFunctionInfo` and adds `contexts` and `docsUrl` (`/docs/filters/{name}`).
- `POST /filters/validate` gains optional `context: "scan" | "backtest" | "chart"`; validation walks
  the AST and reports `function 'x' is not available in {context}` as a normal validation error.
  Callers: `EntrySettingsForm`/`FilterComposer` pass `scan`/`backtest` as appropriate (both today —
  every current function is valid in both, so no behaviour change).
- `IndicatorCalculationService`: unchanged in phase 1 (phase 4 retires `StudyType`).

### 1c. Parity tests (`tests/marketviewer-filters-unit-tests/.../Registry/RegistryParityTests.cs`)

One `[Theory]` per registered descriptor (`MemberData` = `FunctionRegistry.All`):

- name/aliases unique, parseable (`sma(1)` etc. via `Snippet` round-trips through the parser)
- `Signature`, `Snippet`, `Description` non-empty; `Params` count matches what the parser accepts
- `Kind == Series` ⇒ implements `ISeriesFunction` **and** `IIncrementalSeriesFunction`
- `docs/filters/{name}.md` exists (path resolved relative to repo root, same way golden fixtures
  are located) and its frontmatter `name`/`kind`/`contexts` match the descriptor
- golden coverage: `Kind == Series` ⇒ at least one key in every `reference/*.indicators.json`
  starts with `{name}(`; other kinds ⇒ at least one case in `outcomes/filters.json` whose script
  contains `{name}`. (`Load()` in `GoldenFixture.cs:43` already exposes both.)
- `Cost > 0`, `0 < Selectivity <= 1`, `Contexts != None`

Plus a reverse test: every key in the reference files and every outcome script parses against
the current registry (catches renames).

Application-tests: `FilterValidateHandlerUnitTests` — `/filters/functions` returns exactly
`FunctionRegistry.All`; validate rejects an out-of-context function.

Acceptance for phase 1: build green; all existing 493+89 filter tests + application tests pass;
`git grep FunctionCatalog` and `git grep 'new SmaFunction()'` in `ExpressionParser` return nothing;
adding a dummy `[FilterFunction("zzz")]` class with no docs/golden makes exactly the parity tests fail.

## Phase 2 — Docs route + backfill pages for every existing function

- `docs/filters/<name>.md`, one per registered function/keyword (~17: sma, ema, macd, rsi, adv,
  vwap, slope, crosses_over, crosses_under, support_resistance, close, open, high, low, volume,
  float, time) plus `docs/filters/index.md` (DSL overview: comparisons, `[tf,lookback]` brackets,
  `and`/`or`/parens, field access). Migrate content from the package README, fix the stale vwap
  sections, then make the README a stub pointing at `docs/filters/`.
- Page template (also shipped inside the skill as `references/docs-template.md`):

  ```markdown
  ---
  name: rsi
  kind: series
  contexts: [scan, backtest, chart]
  signature: rsi(period)
  since: 2026-08-18
  ---
  # rsi(period)
  One-paragraph plain-English meaning.
  ## Signature & parameters
  ## Formula / algorithm          ← the spec: seed, smoothing (Wilder vs EMA), rounding
  ## Warm-up & seeding             ← how many bars before the first value; what is emitted before that
  ## Forming bar & timeframes      ← behaviour on the current forming candle; per-timeframe notes
  ## Where it can be used          ← scan / backtest / chart, from frontmatter
  ## Examples                      ← 2–3 copyable filters
  ## Gotchas
  ## Changelog
  ```
- Web: `apps/web/src/pages/docs/FilterDocsPage.tsx` at `/docs/filters` and `/docs/filters/:name`
  (public routes, next to the landing page). Load markdown with
  `import.meta.glob('../../../../docs/filters/*.md', { query: '?raw', import: 'default' })`
  (add `docs/` to `server.fs.allow` if Vite complains). Small frontmatter parser + a markdown
  renderer (`react-markdown` + `remark-gfm` — check bundle impact; nothing markdown-related is in
  `apps/web/package.json` today). Index page lists functions grouped by kind with contexts badges.
- `FilterComposer.tsx` autocomplete popover: "Learn more ↗" using `docsUrl` from `/filters/functions`.
- `FILTER_TEMPLATES` in `filterExpression.ts`: keep, but the parity test does not police it (curated).

Acceptance: `/docs/filters/rsi` renders signed-out; parity docs test passes for every function;
package README no longer contradicts the code.

## Phase 3 — The skill + PR template

`.claude/skills/add-filter-function/`

```
SKILL.md                      # frontmatter name/description; when to use; the two paths (add / modify)
references/checklist.md       # the definition-of-done, mirrored in the PR template
references/function-template.cs.md   # ISeriesFunction + IIncrementalSeriesFunction skeleton w/ attribute
references/unit-test-template.md     # per-kind test shapes (series / boolean / keyword)
references/python-reference.md       # how to add compute_reference/compute_outcomes entries; commands;
                                     # "independent implementation, not a port" rule; tolerance guidance
references/docs-template.md          # the page template above
references/modify-existing.md        # CacheVersion bump, S3 entry cache clear, golden re-bless w/ GOLDEN_UPDATE=1,
                                     # changelog entry, memory/plan-11 note
```

SKILL.md flow (add path):

1. Classify: kind (series/boolean/transform/keyword), contexts (chartable?), lookback needs.
2. Write `docs/filters/<name>.md` **first** — it is the spec.
3. Add Python reference (library or from-spec) → run `compute_reference.py` / `compute_outcomes.py`
   → commit regenerated JSONs; check the diff touches only new keys.
4. Implement the C# class with `[FilterFunction]`; series ⇒ `IIncrementalSeriesFunction` using
   `IncrementalSeries` helper; result type in `IIndicatorResult.cs` if multi-field.
5. Unit tests per kind template; perf test if series.
6. Build (per-project — the sln build is known broken), run filters + application + backtest-lambda
   golden tests; parity tests must be green.
7. Optional: `FILTER_TEMPLATES` example; if chartable, `/stocks` wiring (phase 4 removes this step).
8. Self-check against `references/checklist.md`; fill the PR template.

`.github/pull_request_template.md` (or a `filter-function` section in an existing one): the checklist.
`.github/skills/add-filter-function/SKILL.md`: one-line pointer to the `.claude` skill.

## Phase 4 — `/stocks` off `StudyType`

- `StocksHandler`/`IndicatorCalculationService` resolve `Indicators[].type` by name through
  `FunctionRegistry` (only `Contexts.HasFlag(Chart)`), keep the per-result-type `ConvertPoint`
  mapping but key it on result type, not enum. `StudyType` becomes obsolete → delete along with
  `apps/web/src/config/indicators.ts` `REGISTRY` and `types/tools.ts` `IndicatorType`; the
  `IndicatorsModal` lists chartable functions from `/filters/functions?context=chart`.
- `StudyOperandForm.tsx:20–24` hardcoded `<option>` list → same source.

## Open questions (decide during phase 1; not blockers)

- **Required-history metadata.** A new indicator with a long lookback (`sma(390) [1m]`) silently
  under-warms in the backtester (`DataCache.PreviousMinuteSessions = 1`) and the API cache.
  Proposal: `int RequiredBars(args)` on `IFunction` (default = largest int arg) so
  `/filters/validate` can warn "needs N bars of {tf}; only M cached" and `DataCache` can size
  warm-up. Recommend including in phase 1 as a descriptor field + warning; enforcement later.
- Docs markdown location: `docs/filters/` (repo-level, discoverable) vs `apps/web/src/content/`
  (no Vite fs config). Plan assumes `docs/filters/`.
- Whether `contexts` for keywords should distinguish `float` (scalar; scan/backtest but not chart)
  from `close` (series; all). Yes — via `IsScalar` on the keyword descriptor.

## Files touched (summary)

- **Filters package**: new `Registry/` (attribute, descriptor, registry), `ExpressionParser.cs`,
  `FunctionHeuristicsRegistry.cs`, `DataAccessExpression.cs`, every `Functions/**/*.cs` (attribute), `README.md`
- **Application**: `FilterValidateHandler.cs`, `FilterValidateRequest/Response` contracts (+`context`, +`contexts`, +`docsUrl`), later `IndicatorCalculationService.cs`, `StocksHandler.cs`
- **Tests**: new `Registry/RegistryParityTests.cs`, `FilterValidateHandlerUnitTests.cs`
- **Docs**: `docs/filters/*.md`
- **Web**: `pages/docs/FilterDocsPage.tsx`, router, `FilterComposer.tsx`, `filtersApi.ts`, `types/filters.ts`, `vite.config.ts`, `package.json`
- **Skill / process**: `.claude/skills/add-filter-function/**`, `.github/pull_request_template.md`, `.github/skills/add-filter-function/SKILL.md`, `tools/golden/README.md` (point at the skill)
