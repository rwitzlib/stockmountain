# Plan 16 — Billing (Stripe) + credits lifecycle + e2e test suite

Goal: make the backtest experience sellable. Stripe subscription checkout, monthly credit
allocation, one-time credit-pack purchases, lifetime-value tracking, customer-visible billing
history, and a Playwright e2e suite covering billing + backtest flows.

This plan is self-contained: it can be handed to a fresh chat instance with no other context.

## Decisions already made (grilling session 2026-08-24 — do not re-litigate)

1. **Refill trigger is hybrid.** Paid subscribers refill on Stripe's `invoice.paid` webhook —
   aligned to their real billing cycle, and only after money actually arrives. Free-tier users
   refill via a scheduled monthly Lambda. The Lambda derives grant amounts from **tier config,
   never from the stored `MaxCredits`** (existing users have `MaxCredits = 0`; a
   `SET Credits = MaxCredits` reset would top them up to zero forever).
2. **Two credit balances.** `Credits` is the monthly allowance: refill is
   `SET Credits = <tier grant>` (monthly credits do not roll over). New field
   `PurchasedCredits` holds top-up credits: never expires, never reset. Spend order: monthly
   first, then purchased.
3. **Stripe is the system of record for money.** Customer-facing billing history / receipts /
   card management / cancellation = Stripe's hosted **Customer Portal** (no custom billing
   history UI). We additionally keep our own **append-only Dynamo ledger** written from
   webhooks — the LTV source and the audit trail for every credit grant/debit-relevant money
   event. This is the industry-standard split; do not build an invoice UI and do not treat our
   DB as the money source of truth.
4. **All Stripe code lives in the existing API** (Railway): a `BillingController`
   (authenticated) and a `StripeWebhookController` (`[AllowAnonymous]`, signature-verified via
   the Stripe SDK), beside the existing Clerk webhook.
5. **`UserRole` enum renames to `Free / Pro / Premium`** (was `Basic / Advanced / Premium`).
   Stored `Role` strings in user-store contain legacy values — reads must be lenient
   (`Basic`→`Free`, `Advanced`→`Pro`; same pattern as plan 12's legacy-record reads). Stripe
   products carry `tier` metadata mapping product → enum; the landing page keeps its marketing
   labels as display strings.
6. **Plan changes:** upgrade takes effect immediately — `MaxCredits` jumps to the new grant,
   `Credits += (newGrant − oldGrant)`, Stripe prorates the charge. Downgrade is scheduled at
   period end (Stripe-side); the role flips when the renewal webhook lands, credits untouched
   until then.
7. **Failed payment:** during `past_due` the user keeps their role and remaining credits, but
   gets no refill (refill rides `invoice.paid`, so this is automatic). If Stripe's dunning
   exhausts and the subscription is cancelled, `customer.subscription.deleted` drops them to
   Free. No custom grace-period state machine.
8. **Free tier grant is issued at signup** — fix `ClerkUserProvisioningService` defaults
   (`StartingCredits`) from 0 to the Free monthly grant — **100 in the rescaled unit** (see
   decision 9 and Phase 0 results; the landing page's original 100/month promise ends up
   correct after all). **No backfill script**: existing zero-credit users
   are healed by the first monthly Lambda run (which uses tier config, per decision 1).
9. **Credit unit: 1 credit = 100 GB-seconds** (amended 2026-08-24 after phase 0; the earlier
   "keep raw `memoryGB × seconds` and raise grants" choice was superseded the same day).
   Metering divides the raw `memoryGB × seconds` cost by 100 at the source, so a median
   backtest costs ~27 credits and the originally marketed 100 / 1,000 / 5,000 tier numbers
   become literally true. Grant sizes are derived from real `CreditsUsed` data (phase 0
   below). The pre-flight `ESTIMATED_DAILY_CREDIT_COST = 120` (raw units) was above the
   worst per-day cost ever observed and is re-tuned to **0.35/calendar-day in the new unit**
   (= p90 actual).
10. **Top-ups: 2–3 fixed credit packs** (Stripe one-time Prices), priced slightly above the
    subscription's effective per-credit rate so subscribing stays the better deal. **Anyone
    can buy, including Free users** — no subscriber gate.
11. **Billing UI is a dedicated `/billing` page** (new sidebar entry): current tier, both
    balances, upgrade/downgrade CTAs → Stripe Checkout, "Manage billing" → Customer Portal,
    credit-pack purchase cards. `/profile` stays pure Clerk.
12. **E2e: Playwright TypeScript against the deployed dev environment.** Clerk test mode
    (`+clerk_test` emails, fixed OTP `424242`), Stripe test-mode keys with real test Checkout
    (card `4242…`), real backtests against real dev market data. Cadence: **nightly GitHub
    Actions run + `workflow_dispatch`** — not PR-blocking.

## Current-state facts (verified 2026-08-24)

- User store: Dynamo `${team}-${env}-user-store`, PK `Id` = Clerk user id
  (`infra/tf/app/dynamodb.tf:24`). Fields: `Id, Role, IsAdmin, AvatarUrl, IsPublic, Credits,
  MaxCredits, Tokens`. No email, no Stripe fields, no GSI.
  (`packages/marketviewer-infrastructure/MarketViewer.Infrastructure/Services/UserRepository.cs`)
- Debit: `UserRepository.TryDebitCredits` — conditional `SET Credits = Credits − :c` with
  `Credits >= :c`; called once, post-hoc, from
  `apps/backtester/Backtest.Lambda/OrchestratorFunction.cs:155` (backtest flipped to Failed if
  the debit fails). Pre-flight at `OrchestratorFunction.cs:69-84`:
  `Credits < days × 120 → Failed`.
- Metering: per-worker-day cost = `MEMORY_FACTOR (Lambda GB) × elapsedSeconds`
  (`WorkerFunction.cs`), summed by the orchestrator into `CreditsUsed`.
- Provisioning: Clerk webhook (`ClerkWebhookController`, hand-rolled Svix verify) + inline
  fallback in `AuthContextMiddleware`; both funnel to `ClerkUserProvisioningService.Provision`
  (idempotent `if_not_exists` UpdateItem, `StartingCredits = 0`).
- Stripe: **zero code, packages, secrets, or terraform anywhere.**
- Pricing UI: hardcoded `PLANS` in `apps/web/src/pages/LandingPage.tsx:614-659`
  (Free $0 / Pro $29 / Premium $99, "credits reset monthly" copy); every CTA links `/sign-up`.
- Tier enforcement: `[RequiresTier]` policies exist but every controller requires only the
  bottom tier today.
- Scheduled-Lambda pattern to copy: `market_data_aggregator_daily` —
  `aws_cloudwatch_event_rule` + targets in `infra/tf/app/eventbridge.tf`, image-based
  `aws_lambda_function` in `lambda.tf`, `aws_lambda_permission` for events.amazonaws.com, ECR
  repo in `infra/tf/core/ecr.tf`, docker build entry in `.github/workflows/app-deploy.yml`.
- Frontend: Vite + React 18 + TS, react-router, TanStack Query; **no test infra at all** (no
  Playwright/Cypress/vitest). CI: two deploy workflows, no in-repo test gate.

## Design

### Data model

`UserRecord` additions (all optional / default-zero for lenient reads of old records):

- `StripeCustomerId` (S) — set on first checkout; also findable via Stripe customer metadata
  `userId` (store the Clerk id on the Stripe customer at creation).
- `PurchasedCredits` (N, default 0).
- `SubscriptionStatus` (S, optional: `active | past_due | canceled | none`) — display only;
  role remains the enforcement field.

New Dynamo table `${team}-${env}-billing-ledger` (PAY_PER_REQUEST, PITR):

- PK `UserId` (S), SK `EventKey` (S) = `{ISO timestamp}#{stripeEventId}`.
- Attributes: `Type` (`subscription_payment | topup_purchase | refund | monthly_refill |
  signup_grant | upgrade_grant`), `AmountCents` (N, 0 for non-money grants), `Credits` (N,
  signed), `StripeEventId`, `StripeInvoiceId`/`StripePaymentIntentId`, `Tier`, `Description`.
- **Idempotency:** conditional put on `attribute_not_exists(EventKey)`; since the Stripe event
  id is in the SK, webhook redelivery is a no-op. Every webhook handler writes the ledger row
  and applies the credit/role mutation only when the put succeeds.
- LTV for a user = sum of `AmountCents` over their partition. (Admin/reporting query; no UI in
  this plan.)

### Config

Tier config lives in appsettings (per env) + terraform-pushed secrets:

```
Stripe: { SecretKey, WebhookSigningSecret, Prices: { Pro: price_x, Premium: price_y,
          PackSmall: price_a, PackLarge: price_b }, PortalConfigurationId? }
Tiers:  { Free: { MonthlyCredits: N }, Pro: { MonthlyCredits: N }, Premium: { MonthlyCredits: N } }
Packs:  { PackSmall: { Credits: N }, PackLarge: { Credits: N } }
```

Numbers (`N`) come from phase 0. Stripe products/prices are created by hand in the dashboard
(test + live), each product tagged with metadata `tier` / `pack`.

### API surface (`BillingController`, authenticated)

- `POST /billing/checkout-session` `{ kind: "subscription" | "pack", id: "Pro" | "PackSmall" }`
  → creates/reuses the Stripe customer (metadata `userId`), returns Checkout session URL.
  Subscription mode for tiers, payment mode for packs; `client_reference_id = userId`.
- `POST /billing/portal-session` → Customer Portal URL (upgrade/downgrade/cancel/card/history
  all happen there; configure the portal to allow plan switches with proration on upgrade and
  period-end downgrade).
- `GET /billing/summary` → `{ tier, credits, maxCredits, purchasedCredits,
  subscriptionStatus }` for the `/billing` page.

### Webhooks (`StripeWebhookController`, `POST /webhooks/stripe`)

Verify with `EventUtility.ConstructEvent` (Stripe SDK). Handle:

- `checkout.session.completed` — link `StripeCustomerId` to the user; for `payment` mode
  (packs): ledger `topup_purchase` + `ADD PurchasedCredits`.
- `invoice.paid` — ledger `subscription_payment`; resolve tier from the subscription's price;
  `SET Role = tier, MaxCredits = grant, Credits = grant` (monthly credits don't roll over).
  Covers first purchase, renewals, and the period-end leg of downgrades.
- `customer.subscription.updated` — upgrade case (immediately): if new tier > current role, set
  role + `MaxCredits`, `ADD Credits (newGrant − oldGrant)`, ledger `upgrade_grant`; the
  prorated money lands via its own `invoice.paid` (grant nothing extra there — dedupe by
  checking whether the invoice's billing reason is `subscription_update`).
- `customer.subscription.deleted` — drop to Free: `Role = Free`, `MaxCredits = FreeGrant`,
  `Credits = min(Credits, FreeGrant)`, `SubscriptionStatus = canceled`.
- `invoice.payment_failed` — set `SubscriptionStatus = past_due` only.
- `charge.refunded` — ledger `refund` (negative `AmountCents`); credit clawback is a manual
  operator decision, not automated.

### Debit path changes (two balances)

- Pre-flight (`OrchestratorFunction`): `Credits + PurchasedCredits >= estimate`, with the
  re-tuned estimate constant from phase 0.
- `TryDebitCredits(userId, cost)`: read user → compute split (`fromMonthly =
  min(Credits, cost)`, `fromPurchased = cost − fromMonthly`) → conditional UpdateItem
  `SET Credits = :newC, PurchasedCredits = :newP` with condition `Credits = :expectedC AND
  PurchasedCredits = :expectedP`; retry once on conditional failure (concurrent refill/topup),
  else return false. Keep the existing "insufficient at settlement → Failed" semantics.

### Monthly free-tier refill Lambda

New minimal Lambda project `apps/billing/Billing.Lambda` (image-based, same pattern as the
aggregator): scans user-store (paginated), for users with `Role = Free` (or legacy
`Basic`, and `SubscriptionStatus != active`) sets `Credits = FreeGrant, MaxCredits =
FreeGrant` and writes a `monthly_refill` ledger row keyed by `{period}#{userId}` (idempotent —
safe to re-run). EventBridge rule `cron(0 6 1 * ? *)` (1st of month 06:00 UTC). Paid users are
untouched — their refill rides `invoice.paid`.

### Frontend

- `/billing` page + sidebar entry: summary from `GET /billing/summary`; tier cards with
  Subscribe/Upgrade buttons → checkout-session redirect; "Manage billing" → portal-session
  redirect; pack cards → checkout; success/cancel return routes (`/billing?status=success`)
  with a polling refetch (webhook lag ⇒ don't assume instant credit visibility).
- Landing page: signed-in users' CTAs go to `/billing` (or straight to checkout); grant
  numbers in `PLANS` updated to phase-0 values; keep "credits reset monthly" copy honest
  (monthly credits reset; purchased credits don't expire).
- `BacktestSummary` usage meter: show purchased credits when nonzero (e.g. "+N purchased").

### Enum rename

`UserRole`: `Free = 1, Pro = 2, Premium = 3`. Lenient parse for stored strings
(`Basic`→`Free`, `Advanced`→`Pro`) in `UserRepository`/wherever `Enum.Parse` happens; writes
use new names. Update `[RequiresTier]` usages, `TierAuthorizationPolicyProvider`, Optimus'
copy of the repository, and tests. No data migration required.

## Phases

**Phase 0 — economics. ✅ DONE 2026-08-24, see "Phase 0 results" below.** Queried deployed
backtest records' `CreditsUsed` distribution; grants, pack SKUs, and the re-tuned pre-flight
constant are locked and `LandingPage.PLANS` is updated. The `Tiers`/`Packs` config sections
don't exist yet — implement them in phase 2 with the phase-0 numbers.

**Phase 1 — data model + rename. ✅ DONE 2026-08-24 (uncommitted).** `UserRecord` gained
`PurchasedCredits`/`StripeCustomerId`/`SubscriptionStatus`; `UserRole` renamed with
`UserRoleParser` lenient reads (both repository copies); provision defaults 100/100;
two-balance `TryDebitCredits` (atomic decrement fast path, optimistic-concurrency split
path); new `CreditMeter` (`Compute` = GB×s/100, `EstimateForRange` = ceil(days × 0.35))
wired into WorkerFunction (6 sites) + orchestrator pre-flight (now checks
Credits+PurchasedCredits); `billing_ledger` table in dynamodb.tf (terraform validate OK,
apply pending); `UserResponse.PurchasedCredits` + web `UserDetails` type updated;
`tools/billing/rescale_credits_used.py` (dry-run default, idempotent via
CreditUnitVersion=2 — run only AFTER the rescaled lambda deploys). 21 new unit tests;
all touched suites green (infra 18, contracts 7, application 105, api 26, backtest 128).

**Phase 2 — Stripe core. ✅ DONE 2026-08-24 (uncommitted, branch
`feature/stripe-billing-core`).** Stripe.net 52.3.0 in the API. `BillingController`
(`POST /billing/checkout-session`, `POST /billing/portal-session`, `GET /billing/summary`);
checkout for an already-subscribed user returns 400 pointing at the Portal (plan changes are
Portal-side; Checkout would create a second subscription). `StripeWebhookController`
(`[AllowAnonymous]`, `EventUtility.ConstructEvent` with `throwOnApiVersionMismatch: false`;
explicit guard for a missing `Stripe-Signature` header — the SDK NREs instead of throwing
`StripeException`; fails closed with 500 while the signing secret is unset; processor
failure → 500 so Stripe redelivers). `StripeWebhookProcessor` handles the six event types
per the design, each credit/role mutation guarded by the idempotent ledger append; user
resolution = event metadata (`client_reference_id` / subscription metadata / charge
metadata) → Stripe customer `userId` metadata fallback. `IStripeGateway` wraps all Stripe
network calls (customer create/get, checkout + portal sessions) so everything is testable
with constructed events, no network. Checkout sessions stamp `userId` into session,
subscription, and payment-intent metadata. New `IBillingLedgerRepository` +
`BillingLedgerRepository` (conditional put on `attribute_not_exists(EventKey)`);
`IUserRepository` gained `SetStripeCustomerId` / `ApplySubscriptionGrant` /
`ApplyUpgradeGrant` / `AddPurchasedCredits` / `SetSubscriptionStatus` /
`CancelSubscription` (min-clamp via optimistic retry). `BillingCatalog` reads the new
`Tiers` / `Packs` sections (base appsettings.json: 100/1,000/5,000 + 250/1,000) and
`Stripe:Prices`. Terraform: `stripe_*` variables → `Stripe__*` env vars in
`api_aws_environment_variables`, `BillingLedgerConfig__TableName`, ledger table added to
the API IAM allow-list (validate OK, apply pending). Tests: 15 processor fixture tests
(SDK-parsed events), 10 BillingController, 5 signature-verification, 4 ledger, 9 new
user-repo tests — all suites green (api 58, infra 30, contracts 7, application 105,
backtest 128, integration boot).

Manual setup still required before the flow works on dev: create test-mode products/prices
in the Stripe dashboard, register the dev webhook endpoint (`/api/webhooks/stripe`, the six
event types above), then set GitHub secrets `TF_VAR_stripe_secret_key`,
`TF_VAR_stripe_webhook_signing_secret`, `TF_VAR_stripe_price_id_pro` / `_premium` /
`_pack_small` / `_pack_large` (empty values are dropped by the Railway push, so billing
stays fail-closed until then).

**Phase 3 — refill Lambda. ✅ DONE 2026-08-24 (uncommitted, branch
`feature/stripe-billing-core`).** New `apps/billing/Billing.Lambda` (image-based, aggregator
pattern): `RefillFunction` handler (period defaults to current UTC "yyyy-MM"; supports
`{ "period": "...", "dryRun": true }` for manual invokes) + `MonthlyRefillService` — paginated
user-store scan filtered server-side to `Role in (Free, Basic)` and `SubscriptionStatus <>
active`, then per user: idempotent ledger append (`monthly_refill`, EventKey
`{period}#{userId}`) guarding a conditional `SET Credits = :grant, MaxCredits = :grant` that
re-checks eligibility at write time (a user subscribing mid-run is skipped, ledger row
removed). Greptile review (PR #23) found two real P1s, both fixed same day: ledger rows are
now written `Status = pending` and marked applied only after the credit update succeeds
(`BillingLedgerRecord.Status` + `IsPending`/`MarkApplied` on the repository) — an append
collision on a still-pending row resumes the interrupted refill instead of reading as
already-refilled, so a crash or failed rollback between the two writes can no longer
silently cost a user their refill (the SET is idempotent, so resume-over-apply is safe);
and a run that ends with per-user failures now throws `RefillIncompleteException` after
completing the scan, failing the invocation so Lambda's async retry re-runs it (applied
rows no-op, pending rows resume) instead of leaving failed users unrefilled until the next
month's different-period run. Refuses to run when the
configured grant is ≤ 0 (missing Tiers config would otherwise zero every free user). Grant
lives in the Lambda's appsettings (`Tiers:Free:MonthlyCredits` = 100, env-var overridable);
table names from terraform env vars. Reuses `BillingLedgerRepository`/`UserConfig` from the
infrastructure package. Terraform: `billing` ECR repo (core), `billing-refill` lambda +
`billing_lambda` IAM role (user-store Scan/Get/Update, ledger Put/Delete) + EventBridge
`cron(0 6 1 * ? *)` rule/target/permission (app; validate OK, apply pending — core must
apply before the first app deploy so the ECR repo exists). app-deploy.yml gained the billing
docker entry; both solution files updated. 10 new unit tests (Billing.Lambda.UnitTests),
all green. **Remaining: manual invoke on dev after deploy** to verify legacy zero-credit
users get healed (invoke with `{"dryRun": true}` first; result JSON reports
eligible/refilled/alreadyRefilled/skipped/failed).

**Phase 4 — frontend. ✅ DONE 2026-08-24.** `/billing` page (`BillingPage.tsx` + `billingApi.ts`:
summary card with both balances + status badge, tier cards → checkout or portal depending on
subscription state, pack cards, portal button, `?status=success` polling until the webhook
lands with 60s timeout, `?status=cancelled` banner); sidebar `Billing` entry; landing pricing
CTAs → `/billing` when signed in + honest reset copy; `BacktestSummary` shows "+N purchased";
`creditsUsed` displays normalized to 1 decimal.

**Phase 5 — e2e suite. ✅ DONE 2026-08-25 (uncommitted).** New `tests/e2e` Playwright TS
project (own package.json + lockfile; Playwright 1.62, `@clerk/testing` for testing-token
bot-detection bypass + programmatic `email_code` sign-in, `stripe` SDK, Dynamo SDK).
Env-driven config per the design below (`.env.example` + README document every variable);
single worker (shared fixed users + shared dev backend). `billing.spec`: fresh-signup test
uses a per-run throwaway `+clerk_test` address driven through the real `<SignUp/>` UI
(OTP 424242) with best-effort Clerk + user-store cleanup; the subscribe→pack→portal→upgrade
chain runs `mode: 'serial'` with a `beforeAll` that resets the billing user's Dynamo row AND
cancels/deletes their test-mode Stripe customers (found via customer metadata `userId`), so
retries and repeat runs are self-healing. The Pro→Premium upgrade is performed via the
Stripe API (`always_invoice` proration, same shape the Portal produces) rather than
automating Stripe's Portal DOM — what's under test is our `customer.subscription.updated`
handling. `backtest.spec`: fixed 2026-08-03→07 window, `rsi(14) < 30 [5m]` filter through
the real composer; asserts Completed + results render + monthly balance drops by exactly
`creditsUsed` (API-read); insufficient-credits user asserts the Failed state + rejection
banner. Small web change: `BacktestDetailPage` now renders a failure banner from
`backtestEntry.errors` when status is Failed (the "Insufficient credits" message existed in
the record but was never shown). Safety guards: reset helpers refuse user-store tables
without `-dev-` and any non-`sk_test_` Stripe key. CI: `.github/workflows/e2e.yml` —
nightly cron + `workflow_dispatch`, OIDC AWS role for the Dynamo reset, uploads the
Playwright HTML report artifact, not PR-blocking. **Remaining manual setup:** create the
three fixed dev test users (`e2e-billing/backtest/broke+clerk_test@…`, code 424242), then
set GitHub dev-environment secrets `E2E_CLERK_PUBLISHABLE_KEY` / `E2E_CLERK_SECRET_KEY` /
`E2E_STRIPE_SECRET_KEY` and variables `E2E_STRIPE_PRICE_PREMIUM` +
`E2E_{BILLING,BACKTEST,BROKE}_USER_{EMAIL,ID}` (see tests/e2e/README.md). Original scope:

Env-driven config (dev base URL, Clerk test creds, AWS creds for state assertions/reset).
Suites:

- `billing.spec`: signup with `+clerk_test` email → Free grant (100) visible; subscribe Pro via
  test Checkout (4242 card) → role + credits granted (poll); buy pack → purchased balance
  increases; portal opens; upgrade Pro→Premium → immediate bump.
- `backtest.spec`: create a short backtest via the UI → completes → results render → credits
  debited by `creditsUsed`; insufficient-credits user → clear rejection message.
- Out of browser scope (covered as phase-2 integration tests instead): renewal refill,
  period-end downgrade, dunning-cancel — these need Stripe test clocks, not a browser.
- Test-state reset: fixed test-user ids; a setup script uses AWS creds to reset their
  user-store rows (dev only) before each run.
- CI: new `.github/workflows/e2e.yml` — `schedule` (nightly) + `workflow_dispatch`; publishes
  the Playwright HTML report as an artifact. Not PR-blocking.

## Phase 0 results (2026-08-24)

Source: full scan of `stockmountain-dev-backtest-store` (`SK = Context`), 168 records, 161
`Completed` with nonzero `CreditsUsed`, created 2026-07-17 → 2026-08-20. **Caveat: all
records are one power user (the developer) on dev** — these are R&D-usage numbers, not
customer behavior. Revisit after the first real paying cohort.

Observed distribution (credits = `memoryGB × seconds`, summed per worker-day):

| Metric | p25 | p50 | p75 | p90 | p95 | max | mean |
|---|---|---|---|---|---|---|---|
| `CreditsUsed` per backtest | 891 | 2,711 | 5,656 | 12,288 | 20,950 | 47,357 | 5,344 |
| Calendar days per backtest | 181 | 216 | 546 | 947 | 956 | 1,277 | 348 |
| Credits per **calendar** day | 4.9 | 8.3 | 24.1 | 34.9 | 49.6 | 93.1 | 15.9 |
| Wall-clock duration (s) | 17 | 30 | 56 | 106 | 146 | 274 | 46 |

Volume reference: the single power user ran 63 backtests in July and 98 in August (partial),
averaging ~5.3k credits each → a heavy research month ≈ **520k credits**.

### Locked numbers (in the rescaled unit: 1 credit = 100 GB-seconds, decision 9)

The distribution table above is in **raw** units (as stored today); divide by 100 for the
customer-facing unit: median backtest ≈ **27 credits**, p90 ≈ 123, max ≈ 474; credits per
calendar day p50 0.083 / p90 0.349 / max 0.93.

**Metering change (phase 1):** worker day cost becomes `memoryGB × elapsedSeconds / 100`.
**Pre-flight:** `estimated = ceil((calendarDays + 1) × 0.35)` (p90 actual per calendar day;
formula at `OrchestratorFunction.cs:69`). The old raw 120/day exceeded the worst per-day
cost ever observed (raw 93) by 29%. At p90, ~10% of runs may pass pre-flight and cost more
than estimated — the existing settlement-time debit failure path covers that.

**Monthly grants** (also the `MaxCredits` / refill values; signup grant = Free grant) — these
restore the originally marketed landing-page numbers, now literally true:

| Tier | Grant | ≈ median backtests/mo | Effective $ per credit | Rationale |
|---|---|---|---|---|
| Free | **100** | 3–4 | — | Genuine evaluation (median run ≈ 27); pre-flight allows runs up to ~285 calendar days |
| Pro $29 | **1,000** | ~37 | $0.029 | One median backtest per workday with headroom; covers mean-cost (~53) daily use |
| Premium $99 | **5,000** | ~185 | $0.020 | Matches the observed heaviest research month (~5,200); the "this is my job" tier |

**Top-up packs** (one-time, `PurchasedCredits`, never expire; per-credit rate deliberately
above Pro's $0.029 so subscribing stays the better deal):

| Pack | Credits | Price | $ per credit |
|---|---|---|---|
| Small | 250 | $10 | $0.040 |
| Large | 1,000 | $35 | $0.035 |

`LandingPage.PLANS` keeps its original 100 / 1,000 / 5,000 credit lines (briefly changed to
raw-scale numbers during phase 0, reverted same day); FAQ copy anchored to "a few dozen
credits" per typical test. Compute-cost sanity check: 1 credit = 100 GB-seconds ≈ $0.00167
of Lambda, so Pro's 1,000 credits ≈ $1.67 of compute — margins are safe at all tiers.

**Historical records:** the 168 existing dev backtest records store raw-scale `CreditsUsed`
(100× the new unit). All belong to the developer; either leave them (display-only oddity) or
run a one-time ÷100 script in phase 1 — no customer ever sees the seam. Displayed
`creditsUsed` should round to 1 decimal in the UI.

## Risks / notes for implementers

- Webhook ordering is not guaranteed; every handler must be idempotent (ledger conditional
  put is the guard) and tolerate the customer link not existing yet (`checkout.session.completed`
  can lose the race to `invoice.paid` — resolve user via customer metadata `userId` fallback).
- The inline provisioning fallback (`AuthContextMiddleware`) also needs the new 100-credit
  default — it shares `ClerkUserProvisioningService`, so one constant change covers both.
- `.slnx` build is broken repo-wide; build per-project (known issue).
- Don't forget dev vs prod Stripe keys: dev environment gets test-mode keys (required by the
  e2e suite), prod gets live keys. Webhook endpoints registered per environment in the Stripe
  dashboard.
