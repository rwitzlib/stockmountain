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
  /** Stripe test-mode Price id for the Premium tier (upgrade test). */
  premiumPriceId: process.env.E2E_STRIPE_PRICE_PREMIUM,
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

function required(name: string): string {
  const value = process.env[name];
  if (!value) {
    throw new Error(
      `Missing required env var ${name} — see tests/e2e/README.md for setup.`
    );
  }
  return value;
}

/** The three fixed dev test users; rows are reset before each suite runs. */
export function fixedUser(kind: 'BILLING' | 'BACKTEST' | 'BROKE'): FixedUser {
  return {
    email: required(`E2E_${kind}_USER_EMAIL`),
    id: required(`E2E_${kind}_USER_ID`),
  };
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
