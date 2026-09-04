# Plan 17 — Embedded Checkout modal + annual billing

> **Status (Sep 2026):** Phase 1 (the embedded Checkout modal) was reverted.
> Purchases use Stripe's hosted Checkout page again (`success_url`/`cancel_url`
> back to `/billing?status=…`) and existing subscribers change plans in the
> Customer Portal. The web app no longer needs `VITE_STRIPE_PUBLISHABLE_KEY`.
> Phase 2 (annual billing) stands.

Goal: purchases never leave the site (Stripe **Embedded Checkout** rendered in a modal on
`/billing`, replacing the hosted-page redirect), and an **annual billing option** at 20% off
with a bonus-credit sweetener, so more revenue lands up front.

Builds directly on plan 16 (all phases shipped; PRs #21–#27 merged). This plan is
self-contained: it can be handed to a fresh chat instance with no other context, but plan 16's
"Decisions already made" and Design sections describe the system being extended — read them
first if anything below seems under-specified.

## Decisions already made (grilling session 2026-08-26 — do not re-litigate)

1. **Sequencing: embed first, then annual — two PRs.** The e2e hosted-checkout suite
   (stabilized through PR #27) gets rewritten for the iframe exactly once, in the embed PR;
   the annual PR then lands on top of the modal.
2. **Annual discount is 20% off**, priced **Pro $279/yr** ($23.25/mo effective) and
   **Premium $949/yr** ($79.08/mo effective). 9-endings chosen deliberately (19.8% / 20.1%
   effective — close enough to market as "20% off").
3. **Annual credit delivery: the monthly refill Lambda expands to annual subscribers.**
   `invoice.paid` fires once a year for annuals, so it cannot be their refill trigger
   (plan 16 decision 1 assumed monthly invoices). Annual subscribers keep the identical
   monthly-allowance semantics (`SET Credits = grant`, no rollover, same `MaxCredits`);
   the Lambda refills them calendar-monthly (1st, 06:00 UTC) rather than on their signup
   anniversary — accepted inconsistency vs monthly subscribers, invisible in practice.
   **No 12× lump grants.**
4. **Annual bonus: +1 month's grant into `PurchasedCredits`** (Pro +1,000, Premium +5,000;
   never expires) on: **new annual signup, every annual renewal, and monthly→annual
   switches**. NOT on tier upgrades within annual (Pro-annual→Premium-annual gets only the
   existing `upgrade_grant` delta — the year commitment was already rewarded). Marketing
   frame: "20% off + a free month of credits".
5. **Plan switching monthly↔annual allowed in the Customer Portal, both directions.**
   Monthly→annual prorates immediately (upfront cash is the point); annual→monthly is
   scheduled at period end, same as tier downgrades today. Portal configuration gains all
   four subscription prices.
6. **Cancellation: Stripe default.** Cancel at period end, user keeps access for the paid
   year, no automatic refund. Goodwill refunds stay a manual dashboard action (ledger
   already records `charge.refunded`). State the policy plainly in FAQ copy.
7. **Embedded modal covers subscriptions AND packs; `allow_promotion_codes` enabled.**
   One modal component for all four purchasables. Promo codes give a dashboard-managed
   discount lever (launch promos, winbacks) with zero code per promo; stacking with the
   annual price is controlled per-coupon in the Stripe dashboard. The hosted redirect goes
   away entirely; the Customer Portal remains a redirect (Stripe has no embedded portal).
8. **Annual toggle on both `/billing` and the landing page** with per-month framing
   ("$23.25/mo billed annually"). Annual anchoring is most valuable pre-signup.
9. **E2e: rewrite existing specs against the iframe + exactly one new annual test**
   (annual Pro checkout → role + 1,000 monthly + 1,000 bonus purchased). Switches,
   renewals, and refill stay unit/fixture tests — no Stripe test clocks or Portal DOM in
   the browser, per plan 16.

## Current-state facts (verified 2026-08-26, master @ 50bee95)

- Checkout is hosted-redirect: `StripeGateway.CreateCheckoutSession`
  (`apps/api/MarketViewer.Api/Services/Billing/StripeGateway.cs:35`) builds
  `Stripe.Checkout.SessionCreateOptions` with `SuccessUrl`/`CancelUrl`
  (`{ReturnUrlBase}/billing?status=success|cancelled`) and returns `session.Url`;
  `BillingController.CreateCheckoutSession` (`Controllers/Billing/BillingController.cs:32`)
  returns `CheckoutSessionResponse { Url }`; the web redirects
  (`apps/web/src/api/billingApi.ts` `createCheckoutSession` → `window.location`).
- Subscription-tier validation is `Enum.TryParse<UserRole>(request.Id)`
  (`BillingController.cs:44`) — annual ids like `ProAnnual` are not enum values.
- `BillingCatalog` (`Services/Billing/BillingCatalog.cs`) maps `Stripe:Prices` keys
  (`Pro`, `Premium`, `PackSmall`, `PackLarge`) to price ids;
  `TryResolveTierFromPrice` reverse-maps by `Enum.TryParse(key)` — annual price keys will
  not resolve without a change. Webhook processor resolves tier from invoice lines
  (`StripeWebhookProcessor.cs:299`, `line.Pricing.PriceDetails.PriceId`) and subscription
  items.
- `customer.subscription.updated` handling acts only when the new tier is an upgrade;
  same-tier events (which a Pro-monthly→Pro-annual switch produces) are currently no-ops.
- `UserRecord` has no billing-interval field; the refill Lambda
  (`apps/billing/Billing.Lambda`, `MonthlyRefillService`) scans for
  `Role in (Free, Basic) AND SubscriptionStatus <> active` and its appsettings carry only
  `Tiers:Free:MonthlyCredits`.
- Frontend: `BillingPage.tsx` tier/pack cards call `createCheckoutSession` then redirect;
  `?status=success` polling (60s) and `?status=cancelled` banner exist. Landing `PLANS`
  grid is monthly-only. No Stripe JS libraries in `apps/web`; no publishable key anywhere
  (only the secret key, server-side).
- E2e (`tests/e2e`): `billing.spec` drives the **hosted** checkout page (including
  accordion-layout handling from PR #27) for subscribe + pack; the Pro→Premium upgrade is
  done via the Stripe API, not the browser. Premium price resolved at runtime from product
  `tier` metadata.
- Ledger `Type` values: `subscription_payment | topup_purchase | refund | monthly_refill |
  signup_grant | upgrade_grant`. Ledger SK `EventKey` = `{ISO ts}#{stripeEventId}`.

## Design

### Phase 1 — Embedded Checkout modal

Backend (small):

- `CheckoutSessionSpec`: drop `SuccessUrl`/`CancelUrl` (nothing else uses them).
- `StripeGateway.CreateCheckoutSession`: `UiMode = "embedded"`,
  `RedirectOnCompletion = "never"` (success is handled in-page; no return_url needed),
  `AllowPromotionCodes = true`; return `session.ClientSecret` instead of `session.Url`.
- `CheckoutSessionResponse`: `Url` → `ClientSecret`. (`ReturnUrlBase` config stays — the
  portal session still uses it.)

Frontend:

- New deps: `@stripe/stripe-js` + `@stripe/react-stripe-js`. New env
  `VITE_STRIPE_PUBLISHABLE_KEY` (test key on dev, live on prod) — must be added to the web
  build step in the deploy workflow; fail soft (checkout buttons disabled with a message)
  when unset, mirroring the API's fail-closed billing.
- New `CheckoutModal` component: `<EmbeddedCheckoutProvider>` with `fetchClientSecret` =
  `billingApi.createCheckoutSession(kind, id)`; used by both tier and pack cards on
  `/billing`. `onComplete` → the modal closes immediately and the page shows the
  (state-driven) success banner while the existing summary-polling helper runs — the
  payment already succeeded, so nothing in the modal is worth keeping open. Purchase
  buttons stay disabled while the poll runs (see the duplicate-subscription guard below).
  Closing the modal mid-checkout is the new "cancel" — remove the
  `?status=success`/`?status=cancelled` URL handling (the polling helper itself stays,
  retriggered by `onComplete`).
- **Duplicate-subscription guard.** Between payment completing and the webhook landing,
  `SubscriptionStatus` is still not `active`, so the controller's already-subscribed 400
  doesn't fire and a second subscription checkout could create a second real subscription.
  Server-side: before creating a subscription-mode session, resolve the Stripe customer
  first, then ask Stripe (the authoritative record — no local claim state needed) whether
  that customer already has a live/in-flight subscription and 400 if so; checking after
  resolution also covers the first-purchase race path where a request adopts a customer
  another request linked. Frontend: purchase buttons disabled while the post-payment poll
  runs, as UI protection only. Accepted residual risk: two truly concurrent session
  creations before either payment exists are invisible to any Stripe-side check (and a
  local claim would still have to expire on abandoned checkouts); exploiting it requires
  the same user completing two payments in parallel tabs within seconds.
- Embedded Checkout itself needs no origin allow-listing — it works on any origin with the
  publishable key. But **Payment Method Domains registration is separate**: Link, Apple
  Pay, and Google Pay require every domain/subdomain that hosts the payment form to be
  registered (Stripe dashboard → Payment method domains), and Link is currently enabled on
  the account (the e2e Link-opt-out logic exists because of it). Register
  `dev.stockmountain.io` and `stockmountain.io`, or disable those wallet methods.

E2e:

- Port `billing.spec` checkout steps to `page.frameLocator('iframe[name^="embedded-checkout"]')`
  (verify the actual iframe name/title at implementation; Stripe's embed iframe is stable
  but undocumented — anchor on a stable attribute). Card `4242…` entry, email, and the
  accordion-layout handling all move inside the frame locator. The PR #27 Link-opt-out and
  deterministic-wait logic carries over conceptually but every selector changes.
- Success assertion changes from "redirected to `/billing?status=success`" to "checkout
  iframe detaches → page shows the success banner → summary reflects the purchase".

### Phase 2 — Annual billing

Stripe objects (manual dashboard, test + live): two new recurring yearly prices on the
**existing** Pro/Premium products — $279 and $949. Products already carry `tier` metadata,
so webhook/product-level resolution is untouched. Add all four subscription prices to the
Portal configuration's allowed-updates list; proration on upgrade/interval-shortening,
period-end on downgrade (Stripe's interval-switch defaults match decision 5 — verify in
test mode).

Config / terraform:

- `Stripe:Prices` gains `ProAnnual` / `PremiumAnnual`. New terraform vars →
  `TF_VAR_stripe_price_id_pro_annual` / `_premium_annual` → `Stripe__Prices__ProAnnual` /
  `__PremiumAnnual` env vars (same pipeline as existing price ids; empty ⇒ annual buttons
  fail closed via the existing `TryGetPriceId` guard).

Catalog / controller:

- `BillingCatalog`: annual awareness — `TryResolveTierFromPrice` maps `ProAnnual`→`Pro`
  etc. (strip the `Annual` suffix before `Enum.TryParse`; pack keys unaffected because
  they never enum-parse), plus new `IsAnnualPrice(priceId)` /
  `TryGetInterval(priceId, out interval)` for the webhook processor.
- `BillingController`: subscription-id validation becomes catalog-driven (accept
  `Pro | Premium | ProAnnual | PremiumAnnual`; reject `Free` and unknowns) instead of raw
  `Enum.TryParse`. `billingApi.ts` `CheckoutItemId` union gains the two annual ids.

Data model:

- `UserRecord.BillingInterval` (S, optional: `month | year`) — set wherever webhooks set
  role/status from a subscription (`invoice.paid`, `customer.subscription.updated`),
  cleared on `customer.subscription.deleted`. Lenient read (absent = `month`-ish legacy;
  only `year` matters).
- New ledger `Type`: `annual_bonus`. When the same Stripe event also writes
  `subscription_payment`, the bonus row's `EventKey` gets a `#bonus` suffix so both
  conditional puts are independently idempotent.

Webhook processor (the bonus rule, per decision 4):

- `invoice.paid` with billing_reason `subscription_create` or `subscription_cycle` where
  the tier line's price is annual → after the normal grant, `ADD PurchasedCredits += tier
  monthly grant`, ledger `annual_bonus`. (Renewal and first-signup cases.)
- `customer.subscription.updated` where `previous_attributes` shows the plan interval
  changed month→year (verify the exact `previous_attributes.items` shape with an
  SDK-parsed fixture — this is the fiddliest bit) → grant the bonus there, ledger
  `annual_bonus` keyed by that event id. This handler must also act on **same-tier**
  interval switches (today it only handles upgrades): set `BillingInterval`, grant bonus;
  role/credits untouched. The prorated `invoice.paid` (billing_reason
  `subscription_update`) grants nothing, matching plan 16's existing dedupe rule.
- Annual-to-annual tier upgrade: interval unchanged ⇒ no bonus; existing `upgrade_grant`
  path applies once tier resolution understands annual prices (the catalog change above).
- `invoice.paid` must also SET `BillingInterval` from the price interval — this is what
  flips a period-end annual→monthly downgrade back to `month` at renewal.

Refill Lambda:

- Eligibility becomes: (`Role in (Free, Basic)` AND `SubscriptionStatus <> active`) **OR**
  (`SubscriptionStatus = active` AND `BillingInterval = year`). Annual users get
  `SET Credits = <tier grant>, MaxCredits = <tier grant>` from tier config; Lambda
  appsettings `Tiers` section gains `Pro: 1000` / `Premium: 5000`. Same
  `monthly_refill` ledger key `{period}#{userId}`, same pending/applied resume semantics.
- Anniversary-month overlap (Lambda on the 1st + `invoice.paid` on the renewal date) is
  harmless: both are `SET Credits = grant` (not ADD) with independent ledger keys. The
  renewal's `annual_bonus` is an ADD but rides the invoice event's idempotency. Note it in
  a comment; no dedupe code needed.
- IAM unchanged (same tables, same actions).

Frontend:

- Monthly/Annual toggle on `/billing` tier cards and landing `PLANS`: annual shows
  "$23.25/mo billed annually ($279/yr)" / "$79.08/mo billed annually ($949/yr)", a "20%
  off + 1 month of bonus credits" badge, and honest copy (bonus credits never expire —
  they're purchased-balance; the monthly allowance still resets monthly; cancel = access
  through period end, no refund). Subscribe buttons pass `ProAnnual`/`PremiumAnnual`.
- Existing subscribers still see "Manage billing" → Portal for switches (no in-app switch
  UI; the 400 guard in the controller already points there).

E2e (one new test, per decision 9):

- Annual Pro checkout through the modal → poll until `Role = Pro`, `Credits = 1000`,
  `MaxCredits = 1000`, `PurchasedCredits` increased by exactly 1000. Resolve the annual
  price at runtime from the Pro product's prices filtered to `interval = year` (extends the
  existing runtime-resolution pattern; no new env config). The serial billing chain's
  reset helper already cancels/deletes Stripe customers — annual subscriptions are covered
  by the same cleanup.

Tests (non-browser):

- Processor fixture tests: annual signup invoice (bonus granted), annual renewal (bonus
  granted, refill SET), `subscription_update` invoice after a switch (no double grant,
  no bonus), subscription.updated interval-switch event (bonus + `BillingInterval`,
  same-tier), annual→annual upgrade (delta only, no bonus), renewal after period-end
  annual→monthly downgrade (`BillingInterval` back to `month`, no bonus).
- Refill Lambda tests: annual-active included, monthly-active still excluded, past_due
  annual excluded (keeps plan 16 decision 7: no refill during dunning).
- Catalog tests: suffix mapping, `IsAnnualPrice`, pack keys unaffected.
- Controller tests: annual ids accepted, `Free`/garbage rejected.

## Phases

**Phase 1 — embedded checkout modal (PR 1).** Gateway `ui_mode`/client-secret + promo
codes; `CheckoutSessionResponse` contract change; web modal + publishable-key env +
deploy-workflow build arg; delete `?status=` handling; port e2e specs to the iframe.
Deployable alone — hosted checkout is gone the moment this lands, so verify on dev with a
real test purchase before merging (nightly e2e also covers it).

**Phase 2 — annual billing (PR 2).** Dashboard prices + portal config (manual, first);
config/terraform price ids; catalog + controller + contracts; `BillingInterval` +
`annual_bonus` ledger type; webhook bonus/interval logic; refill-Lambda eligibility +
tier grants; `/billing` + landing toggle; one e2e annual test; fixture/unit tests above.

## Manual setup checklist (dev, then prod at launch)

1. Stripe dashboard: create yearly prices $279 (Pro) / $949 (Premium) on existing
   products, test + live.
2. Portal configuration: add all four subscription prices to allowed switches; confirm
   monthly→annual prorates immediately and annual→monthly schedules at period end.
3. GitHub secrets: `TF_VAR_stripe_price_id_pro_annual`, `TF_VAR_stripe_price_id_premium_annual`,
   and the web build's `VITE_STRIPE_PUBLISHABLE_KEY` (test key on dev).
4. Terraform apply (API env vars).
5. Payment method domains (phase 1): register `dev.stockmountain.io` (test mode) and
   `stockmountain.io` (live) under Stripe dashboard → Payment method domains — required
   for Link/Apple Pay/Google Pay inside the embedded form; embedded Checkout itself needs
   no origin configuration.
6. After phase 2 deploys: one manual dev run-through of monthly→annual switch in the
   Portal (browser, unautomated) to confirm the bonus + interval flip land.

## Risks / notes for implementers

- **The iframe rewrite is the riskiest chunk.** Stripe's embedded-checkout iframe
  internals are undocumented; anchor frame selection on a stable attribute and expect the
  same Link/accordion quirks PR #27 fought, now inside a frame locator. Budget e2e
  stabilization time in the phase-1 PR, not after.
- `customer.subscription.updated` `previous_attributes` for an interval switch: build the
  fixture from a real test-mode event capture (Stripe CLI `stripe listen` or dashboard
  event log), not from guessed JSON — the items/plan shape under partial updates is easy
  to get wrong.
- The bonus is an `ADD` on `PurchasedCredits` — it must ride the ledger conditional put
  (grant only when the `annual_bonus` row wins the race), same discipline as every other
  mutation in the processor.
- Suffix-stripping in `TryResolveTierFromPrice` must not accidentally map a future key
  like `AnnualPromo` — match the exact `{Tier}Annual` pattern.
- Publishable key is not a secret in the security sense, but dev/prod separation matters:
  a live key on dev would make e2e purchases real. Reuse the existing `sk_test_` guard
  idea: e2e refuses to run unless the page's key starts `pk_test_`.
- Landing-page pricing copy is marketing surface — run the annual framing through the
  same honest-copy bar as plan 16 ("credits reset monthly" stays true; bonus credits are
  purchased-balance and never expire).
