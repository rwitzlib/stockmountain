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
 * Resolve a tier's subscription price from the Stripe test-mode catalog via
 * the product's `tier` metadata (part of the required dashboard setup, and
 * how the webhook processor maps products back to tiers). Looked up at
 * runtime so the suite needs no per-environment price-id config. Since plan
 * 17 each product carries both a monthly and a yearly price, so resolution
 * filters on the recurring interval instead of trusting default_price.
 */
export async function findPriceIdForTier(
  tier: 'Pro' | 'Premium',
  interval: 'month' | 'year' = 'month'
): Promise<string> {
  const stripe = stripeClient();
  const products = await stripe.products.search({
    query: `active:'true' AND metadata['tier']:'${tier}'`,
  });
  const product = products.data[0];
  if (!product) {
    throw new Error(
      `No active Stripe product tagged metadata tier=${tier} — is the test-mode dashboard setup complete?`
    );
  }
  const prices = await stripe.prices.list({ product: product.id, active: true, limit: 100 });
  const price = prices.data.find((p) => p.recurring?.interval === interval);
  if (!price) {
    throw new Error(`Stripe product ${product.id} (tier=${tier}) has no active ${interval}ly price`);
  }
  return price.id;
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
 * Get past Stripe Link's "Confirm it's you" one-time-code prompt if Checkout
 * shows it. Link accounts are keyed by email across every Stripe merchant, so
 * once one exists for a fixed test address (as happened for the billing user
 * on 2026-09-13) the hosted page prompts for a Link login in one of two
 * shapes: a modal dialog over the card form right after the email is typed
 * (first purchase), or an inline panel that replaces the whole payment form
 * when the customer email is already known (later purchases). The modal's
 * overlay swallows every click, including submit; the panel has no card
 * fields until "Pay without Link" is chosen. Bypassing keeps the test on the
 * plain card path; logging in would pay with Link's saved method instead,
 * which is not what we exercise.
 *
 * Waits up to `timeout` for the prompt; returns whether one was dismissed.
 */
async function bypassLinkLogin(page: Page, timeout: number): Promise<boolean> {
  const codeInput = page.getByRole('textbox', { name: /Security code character/i }).first();
  const appeared = await codeInput
    .waitFor({ state: 'visible', timeout })
    .then(() => true)
    .catch(() => false);
  if (!appeared) return false;

  const payWithoutLink = page.getByRole('button', { name: /Pay without Link/i });
  if (await payWithoutLink.isVisible().catch(() => false)) {
    await payWithoutLink.click();
  } else {
    await page.getByRole('dialog').getByRole('button', { name: 'close' }).click();
  }
  await codeInput.waitFor({ state: 'hidden', timeout: 10_000 });
  return true;
}

/**
 * Hydration gate + layout detection in one step: Checkout renders either the
 * card fields directly, or a payment-method accordion (Card / Cash App /
 * Klarna / wallets) whose card fields only exist after selecting "Card", or —
 * when a Link account exists for the customer's email — a Link login panel
 * with no card fields at all. A one-shot probe can't tell any of these from
 * "still hydrating", so race the layout signals and let whichever renders
 * first decide. Returns once the card fields are visible.
 */
async function waitForCardFields(page: Page): Promise<void> {
  const cardNumber = page.locator('#cardNumber');
  const cardRadio = page
    .locator('input[type="radio"][value="card"]')
    .or(page.getByRole('radio', { name: 'Card' }))
    .first();
  const linkCode = page.getByRole('textbox', { name: /Security code character/i }).first();
  const directLayout = cardNumber.waitFor({ state: 'visible', timeout: 30_000 });
  const accordionLayout = cardRadio.waitFor({ state: 'attached', timeout: 30_000 });
  const linkLayout = linkCode.waitFor({ state: 'visible', timeout: 30_000 });
  // Observe every rejection: the losing waiters time out later and would
  // otherwise surface as unhandled rejections.
  directLayout.catch(() => {});
  accordionLayout.catch(() => {});
  linkLayout.catch(() => {});
  await Promise.race([directLayout, accordionLayout, linkLayout]).catch(() => {
    throw new Error(
      'Stripe Checkout rendered neither card fields, a Card payment-method option, nor a Link login'
    );
  });

  // The race just settled, so the prompt is either on screen now or not coming.
  if (await bypassLinkLogin(page, 500)) {
    // The card form only mounts after opting out of Link; detect its shape
    // afresh (this can't recurse forever — the prompt is gone now).
    await waitForCardFields(page);
    return;
  }

  if (!(await cardNumber.isVisible().catch(() => false))) {
    // The radio input itself may be visually hidden behind its label; force-check.
    await cardRadio.check({ force: true }).catch(async () => {
      await page.getByText('Card', { exact: true }).first().click();
    });
    await cardNumber.waitFor({ state: 'visible', timeout: 15_000 });
  }
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

  await waitForCardFields(page);
  const cardNumber = page.locator('#cardNumber');

  // Contact info renders as a required email input (first purchase — our
  // Stripe customers are created without an email, the user store has none)
  // or as static text showing the customer's saved email (later purchases).
  // Race the two signals so a slow mount can't cause a silent skip.
  if (options.email) {
    const emailInput = page.locator('input[name="email"]');
    const editableEmail = emailInput.waitFor({ state: 'visible', timeout: 15_000 });
    const prefilledEmail = page
      .getByText(options.email)
      .first()
      .waitFor({ state: 'visible', timeout: 15_000 });
    editableEmail.catch(() => {});
    prefilledEmail.catch(() => {});
    await Promise.race([editableEmail, prefilledEmail]).catch(() => {
      throw new Error(
        `Stripe Checkout rendered neither an email input nor the customer email (${options.email})`
      );
    });
    if (await emailInput.isVisible().catch(() => false)) {
      await emailInput.fill(options.email);
      // Link looks the address up as soon as it is entered; the dialog lands
      // within about a second when an account exists.
      await bypassLinkLogin(page, 3_000);
    }
  }

  await cardNumber.fill(TEST_CARD);
  await page.locator('#cardExpiry').fill('12 / 34');
  await page.locator('#cardCvc').fill('123');
  await page.locator('#billingName').fill('StockMountain E2E');
  const postalCode = page.locator('#billingPostalCode');
  if (await postalCode.isVisible().catch(() => false)) {
    await postalCode.fill('54301');
  }

  // "Save my information" (Link) defaults on and its empty phone field blocks
  // submission. Opt out last — the box only renders once the form is active.
  // If the opt-out verifiably fails, satisfying the phone requirement is the
  // only way forward, so that fallback is mandatory (not best-effort): a
  // throw here beats a silent 90s wait for a redirect that never comes.
  // The box is absent altogether once a Link login was bypassed above, so
  // the probe is bounded: it is on screen within milliseconds when it exists.
  const saveInfo = page
    .getByRole('checkbox', { name: /Save my information/i })
    .or(page.locator('#enableStripePass'))
    .first();
  if (await saveInfo.isChecked({ timeout: 3_000 }).catch(() => false)) {
    await saveInfo.uncheck({ force: true }).catch(() => {});
    if (await saveInfo.isChecked().catch(() => false)) {
      const phone = page.locator('#phoneNumber');
      await phone.waitFor({ state: 'visible', timeout: 5_000 });
      await phone.fill('(201) 555-0123');
    }
  }

  // Last look before submitting: Link can re-prompt after the card fields
  // are touched, and the overlay would otherwise intercept the click.
  await bypassLinkLogin(page, 1_000);
  await page.getByTestId('hosted-payment-submit-button').click();
  await page.waitForURL((url) => url.host !== 'checkout.stripe.com', { timeout: 90_000 });
}
