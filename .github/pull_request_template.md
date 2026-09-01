## Summary

<!-- What and why. Link the plan/issue. -->

## Verification

<!-- Commands run and results (build per project — the sln build is known-broken). -->

## Docs sync

<!-- Per the AGENTS.md docs-sync table: which registry/runbook/system-map/ledger rows this
     PR updates, or "none needed". New limits, env vars, storage keys, wide-event fields,
     failure modes, and finished plans all have a home. -->

---

<details>
<summary>Filter function checklist — only if this PR adds or changes a filter DSL function/keyword (delete otherwise)</summary>

Skill: `.claude/skills/add-filter-function/SKILL.md`. Enforced items are asserted by `RegistryParityTests`.

**Spec & reference**
- [ ] `docs/filters/<name>.md` written first; Formula / Warm-up / Forming-bar sections implementable without the C#; `docs/filters/index.md` row added
- [ ] Python reference is independent (library or numpy/pandas from the spec), not a port of the C#
- [ ] `compute_reference.py` / `compute_outcomes.py` re-run; golden JSON diff touches only new keys/cases (or the diff is explained below for a semantics change)

**Implementation**
- [ ] `[FilterFunction]` complete (Signature/Snippet/Description/Params/Fields/Cost/Selectivity/Contexts)
- [ ] Series/transform implements `IIncrementalSeriesFunction`; last cached point always recomputed; no placeholder values during warm-up
- [ ] Chartable? new result type wired in `IndicatorCalculationService.ConvertPoint`
- [ ] Lookback stated on the docs page

**Tests**
- [ ] Unit tests (happy path, bad args, too-few-bars, edge/NaN); perf test for series/transform
- [ ] `dotnet test` green: filters (incl. `RegistryParityTests`), application, backtest-lambda

**Modifying existing semantics only**
- [ ] Docs changelog line; golden diff explanation: <!-- which keys/cases changed and why that is the expected set -->
- [ ] `ScannerService.CacheVersion` bumped
</details>
