import { clerkSetup } from '@clerk/testing/playwright';
import { assertDevSafety, fixedUser } from './src/env';

/**
 * Runs once before the suite: fail fast on unsafe/missing config, then mint
 * the Clerk testing token that setupClerkTestingToken() uses to bypass bot
 * detection. Per-suite user-state resets live in each spec's beforeAll so
 * they re-run when a serial chain retries.
 */
export default async function globalSetup(): Promise<void> {
  assertDevSafety();
  // Surface missing user config as one clear error instead of mid-run noise.
  fixedUser('BILLING');
  fixedUser('BACKTEST');
  fixedUser('BROKE');
  await clerkSetup();
}
