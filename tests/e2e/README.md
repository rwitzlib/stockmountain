# StockMountain e2e suite

Playwright tests that exercise the **deployed dev environment** end to end
(plan 16 phase 5): real Clerk test-mode auth, real Stripe test-mode Checkout,
real backtests against dev market data.

- `tests/billing.spec.ts` — signup → Free grant; subscribe Pro via Checkout
  (4242 card); credit-pack top-up; Customer Portal opens; Pro→Premium upgrade
  with immediate credit bump.
- `tests/backtest.spec.ts` — create a short backtest through the UI, wait for
  completion, verify results render and credits are debited by `creditsUsed`;
  a zero-credit user gets a clear "Insufficient credits" rejection.

Renewal refills, period-end downgrades, and dunning-cancel are covered by the
phase-2 webhook integration tests (they need Stripe test clocks, not a
browser).

## One-time setup

1. **Fixed test users.** Sign up three users on dev (any browser) with
   exactly these `+clerk_test` addresses (verification code is always
   `424242`):
   - `e2e_billing+clerk_test@example.com`
   - `e2e_backtest+clerk_test@example.com`
   - `e2e_broke+clerk_test@example.com`

   The emails are hardcoded in `src/env.ts` (`FIXED_USER_EMAILS`); their
   Clerk ids are resolved automatically at startup via `CLERK_SECRET_KEY`,
   so no per-user configuration is needed. A password set at signup is never
   used — tests sign in with the email code.
2. **Keys.** Dev Clerk publishable + secret keys (`CLERK_*`) and the Stripe
   test-mode secret key. No price-id config: the upgrade test resolves the
   Premium price from the Stripe product's `tier` metadata (part of the
   required dashboard setup).
3. **AWS.** Credentials with read/write on the dev user-store (used to reset
   the fixed users' rows before each run).

## Running locally

```sh
cd tests/e2e
npm ci
npx playwright install chromium
cp .env.example .env   # fill in
npm test               # or: npm run test:headed
npm run report
```

## CI

`.github/workflows/e2e.yml` runs the suite nightly and on
`workflow_dispatch` (not PR-blocking) and uploads the Playwright HTML report
as an artifact. It expects, in the `dev` environment:

- secrets: `E2E_CLERK_PUBLISHABLE_KEY`, `E2E_CLERK_SECRET_KEY`,
  `E2E_STRIPE_SECRET_KEY`, `AWS_DEPLOYMENT_ROLE` (already present)

## State & safety

- Tests run with **one worker**; the subscription tests are a serial chain
  whose `beforeAll` resets the billing user (Dynamo row + Stripe test
  customers/subscriptions), so retries and repeat runs start clean.
- The reset helpers refuse to touch a user-store table without `-dev-` in its
  name, and refuse any Stripe key that isn't `sk_test_...`.
- The signup test creates a throwaway `+clerk_test` user per run and deletes
  it (Clerk + user-store row) afterwards, best-effort.
