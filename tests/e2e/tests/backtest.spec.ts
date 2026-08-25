import { expect, test, type Page } from '@playwright/test';
import { getBacktestEntry, getBillingSummary } from '../src/api';
import { signIn } from '../src/clerk';
import { fixedUser, FREE_GRANT } from '../src/env';
import { resetUserRow } from '../src/reset';

// A short fixed window with known-good dev market data (weekdays only), so
// runs are cheap, fast, and deterministic.
const START_DATE = '2026-08-03';
const END_DATE = '2026-08-07';
const ENTRY_FILTER = 'rsi(14) < 30 [5m]';

/** Enough monthly credits for several short runs, reset before each suite run. */
const BACKTEST_USER_CREDITS = 1_000;

/** Drive the create form: date range + one entry filter, then submit. Returns the backtest id. */
async function createBacktest(page: Page): Promise<string> {
  await page.goto('/backtest/create');

  const dateInputs = page.locator('input[type="date"]');
  await dateInputs.first().fill(START_DATE);
  await dateInputs.nth(1).fill(END_DATE);

  const composerInput = page.getByPlaceholder(/Type a filter/);
  await composerInput.fill(ENTRY_FILTER);
  // Debounced /filters/validate must pass before the add is accepted.
  const addButton = page.getByRole('button', { name: 'Add Entry Condition' });
  await expect(addButton).toBeEnabled();
  await addButton.click();
  await expect(page.getByText(/1 entry filter\b/)).toBeVisible();

  await page.getByRole('button', { name: 'Start Backtest' }).click();
  await page.waitForURL(/\/backtest\/[^/]+$/, { timeout: 30_000 });
  return page.url().split('/').pop()!;
}

test.describe('backtest', () => {
  test.beforeAll(async () => {
    await resetUserRow(fixedUser('BACKTEST').id, {
      role: 'Free',
      credits: BACKTEST_USER_CREDITS,
      maxCredits: BACKTEST_USER_CREDITS,
      purchasedCredits: 0,
    });
    await resetUserRow(fixedUser('BROKE').id, {
      role: 'Free',
      credits: 0,
      maxCredits: FREE_GRANT,
      purchasedCredits: 0,
    });
  });

  test('a short backtest completes, renders results, and debits creditsUsed', async ({ page }) => {
    await signIn(page, fixedUser('BACKTEST').email);
    const before = await getBillingSummary(page);
    expect(before.credits).toBeGreaterThan(0);

    const backtestId = await createBacktest(page);

    // The detail page polls while Pending/InProgress; wait on the API for the
    // terminal state, then confirm the UI caught up.
    await expect
      .poll(async () => (await getBacktestEntry(page, backtestId)).status, {
        timeout: 300_000,
        message: 'orchestrator should complete the backtest',
      })
      .toBe('Completed');
    await expect(page.getByText('Completed', { exact: true }).first()).toBeVisible({
      timeout: 30_000,
    });

    // Results rendered — Share is only enabled once trading data is loaded
    // for a Completed run.
    await expect(page.getByRole('button', { name: 'Share' })).toBeEnabled({
      timeout: 30_000,
    });

    const entry = await getBacktestEntry(page, backtestId);
    expect(entry.status).toBe('Completed');
    expect(entry.creditsUsed).toBeGreaterThan(0);

    // Settlement debit: monthly balance drops by exactly creditsUsed
    // (float storage → small tolerance).
    const after = await getBillingSummary(page);
    expect(before.credits - after.credits).toBeCloseTo(entry.creditsUsed, 1);
    expect(after.purchasedCredits).toBe(before.purchasedCredits);
  });

  test('a user without credits gets a clear rejection', async ({ page }) => {
    await signIn(page, fixedUser('BROKE').email);
    const backtestId = await createBacktest(page);

    // Pre-flight rejection happens async in the orchestrator; the page polls
    // its way to the Failed state.
    await expect
      .poll(async () => (await getBacktestEntry(page, backtestId)).status, {
        timeout: 120_000,
        message: 'pre-flight should fail the backtest',
      })
      .toBe('Failed');
    await expect(page.getByText('Failed', { exact: true }).first()).toBeVisible({
      timeout: 30_000,
    });
    await expect(page.getByText(/Insufficient credits/)).toBeVisible({ timeout: 30_000 });
  });
});
