# Agent guide

You are working in a system designed to be driven by agents. The documentation is a
linked tower — each level answers one kind of question, states facts exactly once, and
anchors every claim to code. Trust anchors over prose; when code and a doc disagree, the
doc has a bug — fix it in the same PR.

## Orientation protocol

Read in this order, stopping when you have what your task needs:

1. [docs/system.md](docs/system.md) — what runs where, the three core flows, process-level
   invariants. One read gives you the topology.
2. [docs/registry.md](docs/registry.md) — limits, retry budgets, error propagation,
   storage keys, env vars, credits. The single home for cross-cutting facts.
3. Then route by task:
   - **Debugging / an error in hand** → [docs/runbook.md](docs/runbook.md) (symptom-indexed),
     then [docs/observability.md](docs/observability.md) (wide-event fields, queries).
   - **Feature work** → [plans/README.md](plans/README.md) lifecycle ledger first (a plan
     may already cover it, or be done); settled arguments are in [docs/adr/](docs/adr/) —
     don't re-litigate them.
   - **Filter DSL work** → `docs/filters/` + the PR template's filter checklist; golden
     tests are the safety net; semantics changes bump `ScannerService.CacheVersion`.

Tense discipline across the tower: docs/ describes what **is**; docs/adr/ records what
**was decided** and why; plans/ describes what is **intended**. Never encode the present
in a plan or the future in the system map.

## Verification contract

The solution-wide build is known-broken; build and test **per project**:

```bash
dotnet build apps/backtester/Backtest.Lambda/Backtest.Lambda.csproj
dotnet test tests/backtest-lambda-unit-tests/Backtest.Lambda.UnitTests/Backtest.Lambda.UnitTests.csproj
# other suites live under tests/<area>-unit-tests/, one per package/app
```

Web: `cd apps/web && npm install && npm run dev`. E2E: `tests/e2e` (Playwright).
Run the suites for every project your diff touches before pushing — CI builds all docker
images on any push under `apps/**`, `packages/**`, `tests/**`, `infra/tf/app/**`.

### Sandbox bootstrap (remote/container sessions)

- No dotnet SDK preinstalled. `builds.dotnet.microsoft.com` may be proxy-blocked;
  `packages.microsoft.com` works: install `packages-microsoft-prod.deb` for the OS, then
  `apt-get install dotnet-sdk-10.0`.
- `global.json` pins an SDK feature band apt may not carry. Workaround: run
  `dotnet build/test` with the **cwd outside the repo** (global.json resolution starts at
  cwd) and pass absolute csproj paths. Same language/TFM, safe for verification.

## Docs-sync duties (accretion contract)

Documentation only stays load-bearing if changes carry their facts to the right home.
When your diff does any of the following, update the named home **in the same PR**:

| Your change | Update |
|---|---|
| new/changed limit, batch size, retry budget | registry § limits / errors |
| new env var or config key | registry § configuration |
| new S3 prefix, Dynamo table, queue | registry § storage (and `infra/tf/app/`) |
| new wide-event field | observability § wide events |
| new failure mode diagnosed | runbook entry (mechanism → where to look → fix) |
| settled a design argument | new ADR (append-only, next number) |
| changed inter-component contract or flow | system.md flow + inventory |
| finished or started a plan | plans/README ledger row |
| changed filter semantics | bump `ScannerService.CacheVersion`, re-bless golden tests |

## House rules

- Don't create a `.sln`; the repo uses `StockMountain.slnx`.
- Comments state constraints the code can't show — never narration or changelog.
- Facts live in exactly one doc; elsewhere, link. Duplication is how docs rot.
- Prefer self-verifying claims: name the probe artifact or anchor next to the assertion
  so any future session can re-check it with one grep.
