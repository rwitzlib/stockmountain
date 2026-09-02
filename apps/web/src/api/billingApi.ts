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
export type BillingInterval = 'month' | 'year';
/** Subscription price keys accepted by the plan-change endpoints. */
export type PlanId = 'Pro' | 'Premium' | 'ProAnnual' | 'PremiumAnnual';

/** Live Stripe view of the user's subscription; fields beyond hasSubscription are unset when false. */
export interface SubscriptionDetails {
	hasSubscription: boolean;
	tier?: BillingTier;
	interval?: BillingInterval;
	status?: string;
	currentPeriodEnd?: string;
	cancelAtPeriodEnd?: boolean;
	pendingChange?: { tier: BillingTier; interval: BillingInterval; effectiveAt: string };
}

export interface PlanChangePreview {
	/** "immediate": prorated charge now. "period_end": scheduled, nothing charged today. */
	timing: 'immediate' | 'period_end';
	newTier: BillingTier;
	newInterval: BillingInterval;
	amountDueCents: number;
	currency?: string;
	effectiveAt: string;
}

export interface PlanChangeResult {
	/**
	 * "applied": on the new price, webhook in flight. "scheduled": takes effect at effectiveAt.
	 * "requires_action": the prorated payment needs the customer on Stripe's invoice page
	 * (paymentUrl); the plan is unchanged until it's paid.
	 */
	status: 'applied' | 'scheduled' | 'requires_action';
	effectiveAt?: string;
	paymentUrl?: string;
}
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
	},

	getSubscription: async (): Promise<SubscriptionDetails> => {
		const response = await authFetch(`${BASE_URL}/billing/subscription`, {
			method: 'GET'
		});

		if (!response.ok) {
			await throwWithApiErrors(response, 'Failed to load subscription details');
		}

		return await response.json();
	},

	previewPlanChange: async (id: PlanId): Promise<PlanChangePreview> => {
		const response = await authFetch(`${BASE_URL}/billing/plan-change/preview`, {
			method: 'POST',
			body: JSON.stringify({ id })
		});

		if (!response.ok) {
			await throwWithApiErrors(response, 'Failed to preview the plan change');
		}

		return await response.json();
	},

	changePlan: async (id: PlanId): Promise<PlanChangeResult> => {
		const response = await authFetch(`${BASE_URL}/billing/plan-change`, {
			method: 'POST',
			body: JSON.stringify({ id })
		});

		if (!response.ok) {
			await throwWithApiErrors(response, 'Failed to change plan');
		}

		return await response.json();
	},

	cancelScheduledPlanChange: async (): Promise<void> => {
		const response = await authFetch(`${BASE_URL}/billing/plan-change`, {
			method: 'DELETE'
		});

		if (!response.ok) {
			await throwWithApiErrors(response, 'Failed to cancel the scheduled plan change');
		}
	}
};
