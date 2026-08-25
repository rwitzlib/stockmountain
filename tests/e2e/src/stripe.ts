/**
 * Stripe helpers: driving the real test-mode hosted Checkout page in the
 * browser, and test-state management through the Stripe API (customer cleanup
 * between runs, plan upgrades).
 */
import type { Page } from '@playwright/test';
import Stripe from 'stripe';
import { assertDevSafety, env } from './env';

export const TEST_CARD = '4242 4242 4242 4242';

function stripeClient(): Stripe {
  if (!env.stripeSecretKey) {
    throw new Error('STRIPE_SECRET_KEY is not set.');
  }
  assertDevSafety();
  return new Stripe(env.stripeSecretKey);
}

/**
 * Cancel and delete every test-mode Stripe customer created for this user id.
 * Checkout stamps the Clerk id into customer metadata, so this finds customers
 * from previous runs and makes re-subscribing repeatable.
 */
export async function cleanupStripeCustomers(userId: string): Promise<void> {
  if (!env.stripeSecretKey) return; // no key → nothing to clean, tests reset via Dynamo only
  const stripe = stripeClient();
  const customers = await stripe.customers.search({
    query: `metadata['userId']:'${userId}'`,
  });
  for (const customer of customers.data) {
    const subscriptions = await stripe.subscriptions.list({
      customer: customer.id,
      status: 'all',
    });
    for (const subscription of subscriptions.data) {
      if (subscription.status !== 'canceled' && subscription.status !== 'incomplete_expired') {
        await stripe.subscriptions.cancel(subscription.id);
      }
    }
    await stripe.customers.del(customer.id);
  }
}

/**
 * Switch the user's active subscription to a new price, invoicing the
 * proration immediately — the same shape the Customer Portal produces on an
 * upgrade. Driving the Portal UI itself is too brittle (Stripe-owned DOM), and
 * what we're actually testing is our customer.subscription.updated handling.
 */
export async function upgradeSubscription(userId: string, priceId: string): Promise<void> {
  const stripe = stripeClient();
  const customers = await stripe.customers.search({
    query: `metadata['userId']:'${userId}'`,
  });
  for (const customer of customers.data) {
    const subscriptions = await stripe.subscriptions.list({
      customer: customer.id,
      status: 'active',
    });
    const subscription = subscriptions.data[0];
    if (!subscription) continue;
    await stripe.subscriptions.update(subscription.id, {
      items: [{ id: subscription.items.data[0].id, price: priceId }],
      proration_behavior: 'always_invoice',
    });
    return;
  }
  throw new Error(`No active subscription found for user ${userId}`);
}

/**
 * Fill the hosted Checkout card form with the 4242 test card and submit, then
 * wait for the redirect back to the app. Assumes the page is mid-navigation
 * to checkout.stripe.com when called.
 */
export async function completeStripeCheckout(
  page: Page,
  options: { email?: string } = {}
): Promise<void> {
  await page.waitForURL(/checkout\.stripe\.com/, { timeout: 30_000 });

  // Email is prefilled (and read-only) when the session carries a customer;
  // only fill it when Checkout actually asks.
  const email = page.locator('input[name="email"]');
  if (options.email && (await email.isEditable().catch(() => false))) {
    await email.fill(options.email);
  }

  await page.locator('#cardNumber').fill(TEST_CARD);
  await page.locator('#cardExpiry').fill('12 / 34');
  await page.locator('#cardCvc').fill('123');
  await page.locator('#billingName').fill('StockMountain E2E');
  const postalCode = page.locator('#billingPostalCode');
  if (await postalCode.isVisible().catch(() => false)) {
    await postalCode.fill('54301');
  }

  await page.getByTestId('hosted-payment-submit-button').click();
  await page.waitForURL((url) => url.host !== 'checkout.stripe.com', { timeout: 90_000 });
}
