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

interface ProblemDetails {
	title?: string;
	detail?: string;
	traceId?: string;
}

async function throwWithApiErrors(response: Response, fallback: string): Promise<never> {
	// Known failures are plain string arrays, e.g. ["Unknown credit pack 'X'"]. An
	// unhandled exception comes back as RFC 7807 ProblemDetails from the API's
	// GlobalExceptionMiddleware, whose traceId locates the stack trace in the API logs.
	let message = `${fallback} (HTTP ${response.status})`;
	try {
		const body: unknown = await response.json();
		if (Array.isArray(body) && body.length > 0) {
			message = body.join(' ');
		} else if (body && typeof body === 'object') {
			const problem = body as ProblemDetails;
			const parts = [problem.title, problem.detail].filter(Boolean);
			if (parts.length > 0) {
				message = parts.join(' ');
				if (problem.traceId) {
					message += ` (trace ${problem.traceId})`;
				}
			}
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

	/** Returns the Stripe-hosted Checkout page URL to redirect to. */
	createCheckoutSession: async (kind: CheckoutKind, id: CheckoutItemId): Promise<string> => {
		const response = await authFetch(`${BASE_URL}/billing/checkout-session`, {
			method: 'POST',
			body: JSON.stringify({ kind, id })
		});

		if (!response.ok) {
			await throwWithApiErrors(response, 'Failed to start checkout');
		}

		const data: { url: string } = await response.json();
		return data.url;
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
