/**
 * Authenticated API reads for state assertions, executed in-page so they ride
 * the signed-in Clerk session (short-lived tokens make out-of-band auth
 * impractical). The page must be on the app origin with a signed-in user.
 */
import type { Page } from '@playwright/test';
import { env } from './env';

export interface BillingSummary {
  tier: 'Free' | 'Pro' | 'Premium';
  credits: number;
  maxCredits: number;
  purchasedCredits: number;
  subscriptionStatus: 'active' | 'past_due' | 'canceled' | 'none';
  hasBillingAccount: boolean;
}

async function authedGet<T>(page: Page, path: string): Promise<T> {
  return page.evaluate(
    async (url: string) => {
      const clerkGlobal = (
        window as unknown as {
          Clerk?: { session?: { getToken(): Promise<string | null> } };
        }
      ).Clerk;
      const token = await clerkGlobal?.session?.getToken();
      if (!token) throw new Error('No Clerk session token available');
      const response = await fetch(url, {
        headers: { Authorization: `Bearer ${token}` },
      });
      if (!response.ok) throw new Error(`GET ${url} → ${response.status}`);
      return response.json();
    },
    `${env.apiUrl}${path}`
  );
}

export function getBillingSummary(page: Page): Promise<BillingSummary> {
  return authedGet<BillingSummary>(page, '/billing/summary');
}

export function getBacktestEntry(
  page: Page,
  id: string
): Promise<{ id: string; status: string; creditsUsed: number; errors?: string[] }> {
  return authedGet(page, `/backtest/${id}`);
}
