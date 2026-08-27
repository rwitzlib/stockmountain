import { authFetch } from './authToken';
import { API_BASE_URL as BASE_URL } from './apiConfig';

export type BillingTier = 'Free' | 'Pro' | 'Premium';
export type SubscriptionStatus = 'active' | 'past_due' | 'canceled' | 'none';

export interface BillingSummary {
	tier: BillingTier;
	credits: number;
	maxCredits: number;
	purchasedCredits: number;
	subscriptionStatus: SubscriptionStatus;
	/** False until a Stripe customer exists — the portal endpoint rejects without one. */
	hasBillingAccount: boolean;
}

export type CheckoutKind = 'subscription' | 'pack';
export type CheckoutItemId =
	| 'Pro'
	| 'Premium'
	| 'ProAnnual'
	| 'PremiumAnnual'
	| 'PackSmall'
	| 'PackLarge';

async function throwWithApiErrors(response: Response, fallback: string): Promise<never> {
	// Error bodies are plain string arrays, e.g. ["Unknown credit pack 'X'"]
	let message = fallback;
	try {
		const body = await response.json();
		if (Array.isArray(body) && body.length > 0) {
			message = body.join(' ');
		}
	} catch {
		// non-JSON body — keep the fallback
	}
	throw new Error(message);
}

export const billingApi = {
	getSummary: async (): Promise<BillingSummary> => {
		const response = await authFetch(`${BASE_URL}/billing/summary`, {
			method: 'GET'
		});

		if (!response.ok) {
			throw new Error('Failed to fetch billing summary');
		}

		return await response.json();
	},

	/** Returns the client secret for mounting the embedded Checkout session. */
	createCheckoutSession: async (kind: CheckoutKind, id: CheckoutItemId): Promise<string> => {
		const response = await authFetch(`${BASE_URL}/billing/checkout-session`, {
			method: 'POST',
			body: JSON.stringify({ kind, id })
		});

		if (!response.ok) {
			await throwWithApiErrors(response, 'Failed to start checkout');
		}

		const data: { clientSecret: string } = await response.json();
		return data.clientSecret;
	},

	/** Returns the Stripe Customer Portal URL to redirect to. */
	createPortalSession: async (): Promise<string> => {
		const response = await authFetch(`${BASE_URL}/billing/portal-session`, {
			method: 'POST'
		});

		if (!response.ok) {
			await throwWithApiErrors(response, 'Failed to open the billing portal');
		}

		const data: { url: string } = await response.json();
		return data.url;
	}
};
