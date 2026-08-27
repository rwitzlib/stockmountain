/**
 * Stripe helpers: driving the real test-mode embedded Checkout (rendered in a
 * Stripe iframe inside the app's checkout modal), and test-state management
 * through the Stripe API (customer cleanup between runs, plan upgrades).
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
 * Fill the embedded Checkout card form with the 4242 test card and submit,
 * then wait for the app to acknowledge the completed payment (the checkout
 * modal closes and the success banner appears). Assumes the checkout modal
 * was just opened; the form lives in a Stripe-owned iframe inside it.
 */
export async function completeStripeCheckout(
  page: Page,
  options: { email?: string } = {}
): Promise<void> {
  // The iframe src is the one attribute Stripe documents nothing about but
  // cannot change (the embed loads from checkout.stripe.com); anchor on it
  // rather than on generated names/titles.
  const frameSelector = 'iframe[src*="checkout.stripe.com"]';
  await page.locator(frameSelector).waitFor({ state: 'attached', timeout: 30_000 });
  const frame = page.frameLocator(frameSelector);

  // Hydration gate + layout detection in one step: Checkout renders either
  // the card fields directly, or a payment-method accordion (Card / Cash App /
  // Klarna / wallets) whose card fields only exist after selecting "Card".
  // A one-shot probe can't tell "accordion" from "still hydrating", so race
  // the two layout signals and let whichever renders first decide.
  const cardNumber = frame.locator('#cardNumber');
  const cardRadio = frame
    .locator('input[type="radio"][value="card"]')
    .or(frame.getByRole('radio', { name: 'Card' }))
    .first();
  const directLayout = cardNumber.waitFor({ state: 'visible', timeout: 30_000 });
  const accordionLayout = cardRadio.waitFor({ state: 'attached', timeout: 30_000 });
  // Observe both rejections: the losing waiter times out later and would
  // otherwise surface as an unhandled rejection.
  directLayout.catch(() => {});
  accordionLayout.catch(() => {});
  await Promise.race([directLayout, accordionLayout]).catch(() => {
    throw new Error('Stripe Checkout rendered neither card fields nor a Card payment-method option');
  });

  if (!(await cardNumber.isVisible().catch(() => false))) {
    // The radio input itself may be visually hidden behind its label; force-check.
    await cardRadio.check({ force: true }).catch(async () => {
      await frame.getByText('Card', { exact: true }).first().click();
    });
    await cardNumber.waitFor({ state: 'visible', timeout: 15_000 });
  }

  // Contact info renders as a required email input (first purchase — our
  // Stripe customers are created without an email, the user store has none)
  // or as static text showing the customer's saved email (later purchases).
  // Race the two signals so a slow mount can't cause a silent skip.
  if (options.email) {
    const emailInput = frame.locator('input[name="email"]');
    const editableEmail = emailInput.waitFor({ state: 'visible', timeout: 15_000 });
    const prefilledEmail = frame
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
    }
  }

  await cardNumber.fill(TEST_CARD);
  await frame.locator('#cardExpiry').fill('12 / 34');
  await frame.locator('#cardCvc').fill('123');
  await frame.locator('#billingName').fill('StockMountain E2E');
  const postalCode = frame.locator('#billingPostalCode');
  if (await postalCode.isVisible().catch(() => false)) {
    await postalCode.fill('54301');
  }

  // "Save my information" (Link) defaults on and its empty phone field blocks
  // submission. Opt out last — the box only renders once the form is active.
  // If the opt-out verifiably fails, satisfying the phone requirement is the
  // only way forward, so that fallback is mandatory (not best-effort): a
  // throw here beats a silent 90s wait for a completion that never comes.
  const saveInfo = frame
    .getByRole('checkbox', { name: /Save my information/i })
    .or(frame.locator('#enableStripePass'))
    .first();
  if (await saveInfo.isChecked().catch(() => false)) {
    await saveInfo.uncheck({ force: true }).catch(() => {});
    if (await saveInfo.isChecked().catch(() => false)) {
      const phone = frame.locator('#phoneNumber');
      await phone.waitFor({ state: 'visible', timeout: 5_000 });
      await phone.fill('(201) 555-0123');
    }
  }

  // Embedded mode reuses hosted Checkout's submit testid today; the
  // type="submit" fallback survives a rename.
  await frame
    .getByTestId('hosted-payment-submit-button')
    .or(frame.locator('button[type="submit"]'))
    .first()
    .click();

  // No redirect in embedded mode: onComplete closes the modal (detaching the
  // iframe) and the page shows its success banner while it polls for the
  // webhook. Wait for the iframe to go away FIRST — the banner text alone
  // could match a stale banner from an earlier purchase on the same page.
  await page.locator(frameSelector).waitFor({ state: 'detached', timeout: 90_000 });
  await page
    .getByText('Payment received', { exact: false })
    .waitFor({ state: 'visible', timeout: 15_000 });
}
