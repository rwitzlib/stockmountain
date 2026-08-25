/**
 * Env-driven configuration for the e2e suite. Everything targets the deployed
 * dev environment — never production (the reset helpers refuse non-dev tables
 * and non-test Stripe keys as a second line of defense).
 *
 * Required env vars are only read lazily (inside tests/hooks) so that
 * `playwright test --list` and typechecking work without a full .env.
 */

const baseUrl = process.env.E2E_BASE_URL ?? 'https://dev.stockmountain.io';

export const env = {
  baseUrl,
  apiUrl: process.env.E2E_API_URL ?? `${baseUrl}/api`,
  userStoreTable: process.env.E2E_USER_STORE_TABLE ?? 'stockmountain-dev-user-store',
  awsRegion: process.env.AWS_REGION ?? 'us-east-2',
  /** Domain for throwaway `+clerk_test` signup addresses. */
  signupEmailDomain: process.env.E2E_SIGNUP_EMAIL_DOMAIN ?? 'example.com',
  /** Stripe test-mode secret key; enables customer cleanup + the upgrade test. */
  stripeSecretKey: process.env.STRIPE_SECRET_KEY,
};

// Tier grants and pack sizes — mirror Tiers/Packs in the API appsettings and
// the Stripe test products (plan 16 phase 0 locked numbers).
export const FREE_GRANT = 100;
export const PRO_GRANT = 1_000;
export const PREMIUM_GRANT = 5_000;
export const PACK_SMALL_CREDITS = 250;

export interface FixedUser {
  /** `+clerk_test` email of the pre-created dev Clerk user. */
  email: string;
  /** Clerk user id (`user_...`) — the user-store partition key. */
  id: string;
}

export type FixedUserKind = 'BILLING' | 'BACKTEST' | 'BROKE';

/**
 * The three fixed dev test users (pre-created once on the dev Clerk instance,
 * OTP 424242). Emails live in source; their Clerk ids are resolved from these
 * via the backend API in global setup.
 */
export const FIXED_USER_EMAILS: Record<FixedUserKind, string> = {
  BILLING: 'e2e_billing+clerk_test@example.com',
  BACKTEST: 'e2e_backtest+clerk_test@example.com',
  BROKE: 'e2e_broke+clerk_test@example.com',
};

/**
 * The three fixed dev test users; rows are reset before each suite runs.
 * Ids are stashed in process.env by global setup (Playwright propagates env
 * to workers), so this stays synchronous inside tests.
 */
export function fixedUser(kind: FixedUserKind): FixedUser {
  const id = process.env[`E2E_${kind}_USER_ID`];
  if (!id) {
    throw new Error(
      `No resolved Clerk id for the ${kind} test user (${FIXED_USER_EMAILS[kind]}) — ` +
        'global setup should have resolved it; does the user exist on the dev instance?'
    );
  }
  return { email: FIXED_USER_EMAILS[kind], id };
}

export function assertDevSafety(): void {
  if (!env.userStoreTable.includes('-dev-')) {
    throw new Error(
      `Refusing to run: user-store table "${env.userStoreTable}" is not a dev table.`
    );
  }
  if (env.stripeSecretKey && !env.stripeSecretKey.startsWith('sk_test_')) {
    throw new Error('Refusing to run: STRIPE_SECRET_KEY is not a test-mode key.');
  }
}
