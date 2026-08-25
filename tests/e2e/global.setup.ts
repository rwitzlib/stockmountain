import { clerkSetup } from '@clerk/testing/playwright';
import { lookupClerkUserId } from './src/clerk';
import { assertDevSafety, FIXED_USER_EMAILS, FixedUserKind } from './src/env';

/**
 * Runs once before the suite: fail fast on unsafe/missing config, mint the
 * Clerk testing token that setupClerkTestingToken() uses to bypass bot
 * detection, and resolve the fixed test users' Clerk ids from their in-source
 * emails. Ids are stashed in process.env, which Playwright propagates to
 * worker processes — so fixedUser() stays synchronous in tests. Per-suite
 * user-state resets live in each spec's beforeAll so they re-run when a
 * serial chain retries.
 */
export default async function globalSetup(): Promise<void> {
  assertDevSafety();
  await clerkSetup();

  for (const kind of Object.keys(FIXED_USER_EMAILS) as FixedUserKind[]) {
    const email = FIXED_USER_EMAILS[kind];
    const id = await lookupClerkUserId(email);
    if (!id) {
      throw new Error(
        `Fixed test user ${email} does not exist on the dev Clerk instance — ` +
          'create it once via /sign-up (OTP 424242); see tests/e2e/README.md.'
      );
    }
    process.env[`E2E_${kind}_USER_ID`] = id;
  }
}
