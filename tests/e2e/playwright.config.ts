import { defineConfig, devices } from '@playwright/test';
// src/env loads dotenv itself, before its process.env snapshot.
import { env } from './src/env';

/**
 * E2e suite against the deployed dev environment (plan 16 phase 5).
 * Single worker: the suites mutate shared fixed test users and a shared dev
 * backend, so parallelism would race. Nightly + on-demand in CI, not
 * PR-blocking.
 */
export default defineConfig({
  testDir: './tests',
  globalSetup: './global.setup.ts',
  workers: 1,
  fullyParallel: false,
  retries: process.env.CI ? 1 : 0,
  // Backtests legitimately take minutes; per-assertion timeouts stay tight.
  timeout: 360_000,
  expect: { timeout: 15_000 },
  reporter: process.env.CI
    ? [['list'], ['html', { open: 'never' }]]
    : [['list'], ['html', { open: 'on-failure' }]],
  use: {
    baseURL: env.baseUrl,
    locale: 'en-US',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
