# StockMountain e2e suite

Playwright tests that exercise the **deployed dev environment** end to end
(plan 16 phase 5): real Clerk test-mode auth, real Stripe test-mode Checkout,
real backtests against dev market data. Targets `https://dev.stockmountain.io`
by default (`E2E_BASE_URL` to override — never point this at production).

- `tests/billing.spec.ts` — fresh signup via the real sign-up UI → Free grant
  (100) visible; then a serial chain on the fixed billing user: subscribe Pro
  via test Checkout (4242 card, driven inside the **embedded-checkout
  iframe** in the app's modal — plan 17) → role + 1,000 grant land via
  webhook; buy the $10 pack → +250 purchased credits; Customer Portal opens;
  Pro→Premium upgrade → +4,000 immediately (driven through the Stripe API
  rather than the Portal's DOM — what's under test is our
  `customer.subscription.updated` handling).

  The embedded checkout only renders when the deployed web bundle was built
  with `VITE_STRIPE_PUBLISHABLE_KEY` (test-mode `pk_test_…` on dev — a live
  key would refuse the dev API's test-mode sessions).
- `tests/backtest.spec.ts` — create a short backtest through the UI
  (2026-08-03→07, `rsi(14) < 30 [5m]`), wait for completion, verify results
  render and the monthly balance drops by exactly `creditsUsed`; a
  zero-credit user gets the Failed state with an "Insufficient credits"
  banner.

Renewal refills, period-end downgrades, and dunning-cancel are deliberately
out of browser scope: simulating them live needs Stripe test clocks. They're
covered by the phase-2 webhook processor unit tests
(`MarketViewer.Api.UnitTests`, constructed Stripe events).

## Prerequisites

1. **Fixed test users** — exist on the dev Clerk instance (created
   2026-08-25); emails are hardcoded in `src/env.ts` (`FIXED_USER_EMAILS`):
   - `e2e_billing+clerk_test@example.com`
   - `e2e_backtest+clerk_test@example.com`
   - `e2e_broke+clerk_test@example.com`

   Their Clerk ids are resolved at startup via `CLERK_SECRET_KEY` — no
   per-user config, and a missing user fails setup with a clear error. If one
   is ever recreated, sign up with the exact same address (OTP `424242`); any
   password set at signup is never used, tests sign in with the email code.
2. **Clerk instance** — "Email verification code" must be enabled as a
   sign-in factor (the suite signs in with the `email_code` strategy).
3. **Stripe (test mode)** — the phase-2 dashboard setup must be live on dev:
   products/prices created with `tier` / `pack` metadata (the upgrade test
   finds the Premium price via `metadata.tier`), the dev webhook endpoint
   registered, and the API deployed with the Stripe secrets (billing
   endpoints fail closed until then).
4. **AWS credentials** with read/write on the dev user-store (`aws sso login`
   or exported keys locally; OIDC role in CI) — used to reset the fixed
   users' rows.

## Running locally

```sh
cd tests/e2e
npm ci
npx playwright install chromium
cp .env.example .env   # fill in the three keys
npm test               # or: npm run test:headed
npm run report
```

## CI

`.github/workflows/e2e.yml` runs the suite nightly (08:00 UTC) and on
`workflow_dispatch` (not PR-blocking), assumes `AWS_DEPLOYMENT_ROLE` via OIDC
for the Dynamo resets, and always uploads the Playwright HTML report as an
artifact. Total config in the GitHub `dev` environment — three secrets:

- `E2E_CLERK_PUBLISHABLE_KEY` / `E2E_CLERK_SECRET_KEY` (dev Clerk instance)
- `E2E_STRIPE_SECRET_KEY` (test mode)

## State & safety

- **One worker**, no parallelism: the suites mutate shared fixed users and a
  shared dev backend.
- Each suite resets its users in `beforeAll`: billing chain → billing user to
  Free/100 **plus** cancel/delete their test-mode Stripe
  customers/subscriptions (found via customer metadata `userId`); backtest
  suite → backtest user to 1,000 credits, broke user to 0. Serial-chain
  retries restart the chain and re-run the reset, so retries and repeat runs
  are self-healing.
- Guards: reset helpers refuse a user-store table without `-dev-` in its
  name; all Stripe helpers refuse a key that isn't `sk_test_...`.
- The signup test creates a throwaway `+clerk_test` user per run and deletes
  it afterwards (Clerk user + user-store row, best-effort).
