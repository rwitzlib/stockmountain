/**
 * Clerk auth helpers. All test users use `+clerk_test` addresses, which the
 * dev Clerk instance treats as test emails: no real mail is sent and the
 * verification code is always 424242. setupClerkTestingToken bypasses bot
 * detection on the dev instance (requires clerkSetup() in global setup).
 */
import type { Page } from '@playwright/test';
import { clerk, setupClerkTestingToken } from '@clerk/testing/playwright';

export const CLERK_TEST_OTP = '424242';

/** Programmatic email-code sign-in for an existing test user. */
export async function signIn(page: Page, email: string): Promise<void> {
  await setupClerkTestingToken({ page });
  await page.goto('/');
  await page.waitForFunction(() => (window as unknown as { Clerk?: { loaded?: boolean } }).Clerk?.loaded);
  await clerk.signIn({
    page,
    signInParams: { strategy: 'email_code', identifier: email },
  });
}

/**
 * Drive the real <SignUp/> UI — the signup test covers the actual user flow
 * (form → OTP → provisioned with the Free grant), so no programmatic shortcut
 * here. Selectors stay loose because Clerk owns the component DOM.
 */
export async function signUpThroughUi(
  page: Page,
  credentials: { email: string; password: string }
): Promise<void> {
  await setupClerkTestingToken({ page });
  await page.goto('/sign-up');

  const emailInput = page.locator('input[name="emailAddress"], input[type="email"]').first();
  await emailInput.waitFor({ state: 'visible', timeout: 20_000 });
  await emailInput.fill(credentials.email);

  const passwordInput = page.locator('input[name="password"]').first();
  if (await passwordInput.isVisible().catch(() => false)) {
    await passwordInput.fill(credentials.password);
  }

  await page
    .getByRole('button', { name: /continue|sign up/i })
    .first()
    .click();

  // Email verification step: type the fixed test OTP. Clerk renders either a
  // single code input or segmented one-time-code boxes; typing digit-by-digit
  // works for both.
  const otpInput = page
    .locator('input[name="code"], input[autocomplete="one-time-code"]')
    .first();
  await otpInput.waitFor({ state: 'visible', timeout: 20_000 });
  await otpInput.click();
  await page.keyboard.type(CLERK_TEST_OTP, { delay: 60 });

  // Most flows auto-submit on the last digit; click through if a button remains.
  const verifyButton = page.getByRole('button', { name: /continue|verify/i }).first();
  if (await verifyButton.isVisible().catch(() => false)) {
    await verifyButton.click().catch(() => {});
  }

  await page.waitForFunction(
    () => !!(window as unknown as { Clerk?: { user?: unknown } }).Clerk?.user,
    undefined,
    { timeout: 30_000 }
  );
}

/** The signed-in user's Clerk id, straight from clerk-js. */
export async function currentUserId(page: Page): Promise<string> {
  return page.evaluate(() => {
    const clerkGlobal = (window as unknown as { Clerk?: { user?: { id: string } } }).Clerk;
    if (!clerkGlobal?.user) throw new Error('No signed-in Clerk user');
    return clerkGlobal.user.id;
  });
}

/**
 * Best-effort deletion of a throwaway signup user via the Clerk backend API,
 * so nightly runs don't accumulate test users on the dev instance.
 * Returns the deleted user's id, or null if not found / no secret key.
 */
export async function deleteClerkUserByEmail(email: string): Promise<string | null> {
  const secretKey = process.env.CLERK_SECRET_KEY;
  if (!secretKey) return null;
  const headers = { Authorization: `Bearer ${secretKey}` };

  const lookup = await fetch(
    `https://api.clerk.com/v1/users?email_address=${encodeURIComponent(email)}`,
    { headers }
  );
  if (!lookup.ok) return null;
  const users = (await lookup.json()) as { id: string }[];
  const userId = users[0]?.id;
  if (!userId) return null;

  await fetch(`https://api.clerk.com/v1/users/${userId}`, { method: 'DELETE', headers });
  return userId;
}
