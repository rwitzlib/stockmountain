import { expect, test, type Page } from '@playwright/test';
import { getBillingSummary } from '../src/api';
import { deleteClerkUserByEmail, signIn, signUpThroughUi } from '../src/clerk';
import {
  env,
  fixedUser,
  FREE_GRANT,
  PACK_SMALL_CREDITS,
  PREMIUM_GRANT,
  PRO_GRANT,
} from '../src/env';
import { deleteUserRow, resetUserRow } from '../src/reset';
import {
  cleanupStripeCustomers,
  completeStripeCheckout,
  findPriceIdForTier,
  upgradeSubscription,
} from '../src/stripe';

/** Poll the billing summary until `predicate` holds (webhook lag is expected). */
async function waitForSummary(
  page: Page,
  predicate: (summary: Awaited<ReturnType<typeof getBillingSummary>>) => boolean,
  message: string,
  timeout = 90_000
) {
  await expect
    .poll(async () => predicate(await getBillingSummary(page)), { timeout, message })
    .toBe(true);
  return getBillingSummary(page);
}

test.describe('signup', () => {
  // Unique throwaway test address per run; +clerk_test → fixed OTP, no real mail.
  const email = `e2e-signup-${Date.now()}+clerk_test@${env.signupEmailDomain}`;

  test.afterAll(async () => {
    // Best-effort: keep the dev Clerk instance and user-store free of
    // accumulating throwaway users.
    const userId = await deleteClerkUserByEmail(email).catch(() => null);
    if (userId) await deleteUserRow(userId).catch(() => {});
  });

  test('a fresh signup receives the Free monthly grant', async ({ page }) => {
    await signUpThroughUi(page, { email, password: `E2e-${Date.now()}-Pw!` });

    await page.goto('/billing');
    const summary = await waitForSummary(
      page,
      (s) => s.credits === FREE_GRANT,
      'signup grant should land via provisioning',
      60_000
    );
    expect(summary.tier).toBe('Free');
    expect(summary.maxCredits).toBe(FREE_GRANT);
    expect(summary.purchasedCredits).toBe(0);

    await expect(page.getByText(`${FREE_GRANT} / ${FREE_GRANT} left`)).toBeVisible();
  });
});

test.describe('subscription lifecycle', () => {
  // One causal chain on the fixed billing user: subscribe → pack → portal →
  // upgrade. Serial so a retry restarts the chain — and beforeAll re-runs the
  // state reset, making the whole block self-healing.
  test.describe.configure({ mode: 'serial' });

  test.beforeAll(async () => {
    const user = fixedUser('BILLING');
    await cleanupStripeCustomers(user.id);
    await resetUserRow(user.id, {
      role: 'Free',
      credits: FREE_GRANT,
      maxCredits: FREE_GRANT,
      purchasedCredits: 0,
    });
  });

  test('subscribing to Pro through Stripe Checkout grants the Pro allowance', async ({ page }) => {
    const user = fixedUser('BILLING');
    await signIn(page, user.email);
    await page.goto('/billing');

    await page.getByRole('button', { name: 'Subscribe to Pro' }).click();
    await completeStripeCheckout(page, { email: user.email });

    const summary = await waitForSummary(
      page,
      (s) => s.tier === 'Pro',
      'invoice.paid webhook should set the Pro role and grant'
    );
    expect(summary.credits).toBe(PRO_GRANT);
    expect(summary.maxCredits).toBe(PRO_GRANT);
    expect(summary.subscriptionStatus).toBe('active');
    expect(summary.hasBillingAccount).toBe(true);

    // The page's own success-polling should surface the new allowance too.
    await expect(page.getByText('1,000 / 1,000 left')).toBeVisible({ timeout: 15_000 });
  });

  test('buying a credit pack tops up purchased credits', async ({ page }) => {
    const user = fixedUser('BILLING');
    await signIn(page, user.email);
    await page.goto('/billing');

    // The small pack's buy button is labeled with its price.
    await page.getByRole('button', { name: '$10' }).click();
    await completeStripeCheckout(page, { email: user.email });

    const summary = await waitForSummary(
      page,
      (s) => s.purchasedCredits === PACK_SMALL_CREDITS,
      'checkout.session.completed webhook should add the pack credits'
    );
    // Monthly allowance untouched by a top-up.
    expect(summary.credits).toBe(PRO_GRANT);
  });

  test('the Stripe customer portal opens', async ({ page }) => {
    const user = fixedUser('BILLING');
    await signIn(page, user.email);
    await page.goto('/billing');

    await page.getByRole('button', { name: 'Manage billing' }).click();
    await page.waitForURL(/billing\.stripe\.com/, { timeout: 30_000 });
  });

  test('upgrading Pro → Premium bumps the grant immediately', async ({ page }) => {
    test.skip(!env.stripeSecretKey, 'Needs STRIPE_SECRET_KEY');
    const user = fixedUser('BILLING');

    // Plan changes go through the Customer Portal, whose Stripe-owned DOM is
    // too brittle to automate. Reproduce the portal's upgrade through the
    // Stripe API instead — what's under test is our
    // customer.subscription.updated handling, which is identical either way.
    // The Premium price is resolved from the product's tier metadata.
    await upgradeSubscription(user.id, await findPriceIdForTier('Premium'));

    await signIn(page, user.email);
    await page.goto('/billing');
    const summary = await waitForSummary(
      page,
      (s) => s.tier === 'Premium',
      'subscription.updated webhook should apply the upgrade immediately'
    );
    // Upgrade grants the difference on top of the current balance:
    // 1,000 (untouched Pro grant) + (5,000 − 1,000).
    expect(summary.credits).toBe(PRO_GRANT + (PREMIUM_GRANT - PRO_GRANT));
    expect(summary.maxCredits).toBe(PREMIUM_GRANT);
    // Purchased credits are never touched by plan changes.
    expect(summary.purchasedCredits).toBe(PACK_SMALL_CREDITS);

    await expect(page.getByText('5,000 / 5,000 left')).toBeVisible({ timeout: 15_000 });
  });

  test('subscribing to annual Pro grants the allowance plus a bonus month of purchased credits', async ({
    page,
  }) => {
    const user = fixedUser('BILLING');
    // The chain left the user on Premium monthly; annual checkout needs a clean
    // slate, and resetting here keeps the step self-healing on retries too.
    await cleanupStripeCustomers(user.id);
    await resetUserRow(user.id, {
      role: 'Free',
      credits: FREE_GRANT,
      maxCredits: FREE_GRANT,
      purchasedCredits: 0,
    });

    await signIn(page, user.email);
    await page.goto('/billing');

    await page.getByRole('button', { name: 'Annual · 20% off' }).click();
    await page.getByRole('button', { name: 'Subscribe to Pro' }).click();
    await completeStripeCheckout(page, { email: user.email });

    // The annual bonus is a second ledger mutation on the same invoice.paid
    // event, so poll until both the grant and the bonus have landed.
    const summary = await waitForSummary(
      page,
      (s) => s.tier === 'Pro' && s.purchasedCredits === PRO_GRANT,
      'invoice.paid webhook should set the Pro role, monthly grant, and annual bonus'
    );
    expect(summary.credits).toBe(PRO_GRANT);
    expect(summary.maxCredits).toBe(PRO_GRANT);
    // The +1-month commitment bonus lands in the never-expiring purchased
    // balance (reset to 0 above, so "increased by exactly one month's grant").
    expect(summary.purchasedCredits).toBe(PRO_GRANT);
    expect(summary.subscriptionStatus).toBe('active');
  });
});
