import { useEffect, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useUser } from '@clerk/react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Check, CreditCard, ExternalLink, Loader2, X } from 'lucide-react';
import {
  billingApi,
  BillingSummary,
  BillingTier,
  CheckoutItemId,
  CheckoutKind,
} from '../api/billingApi';
import { Button } from '../components/ui/button';
import { toast } from '../hooks/use-toast';
import { cn } from '../utils/utils';

// Display copy for the tier/pack cards. Prices and grants mirror the Stripe
// products and the Tiers/Packs config in the API (appsettings.json).
const TIER_PLANS: {
  id: BillingTier;
  price: string;
  period?: string;
  credits: number;
  tagline: string;
  features: string[];
  highlighted?: boolean;
}[] = [
  {
    id: 'Free',
    price: '$0',
    credits: 100,
    tagline: 'Kick the tires. Keep the charts.',
    features: ['100 credits / month', 'Full charting workspace', '1 paper trading bot'],
  },
  {
    id: 'Pro',
    price: '$29',
    period: '/mo',
    credits: 1000,
    tagline: 'For traders building a real playbook.',
    highlighted: true,
    features: ['1,000 credits / month', '10 paper trading bots', 'Strategy optimizer', 'Masked sharing'],
  },
  {
    id: 'Premium',
    price: '$99',
    period: '/mo',
    credits: 5000,
    tagline: 'For traders ready to go live.',
    features: ['5,000 credits / month', 'Unlimited paper trading bots', 'Live trading — early access', 'Priority queue'],
  },
];

const PACKS: { id: CheckoutItemId; name: string; credits: number; price: string; note: string }[] = [
  { id: 'PackSmall', name: 'Small pack', credits: 250, price: '$10', note: '$0.040 / credit' },
  { id: 'PackLarge', name: 'Large pack', credits: 1000, price: '$35', note: '$0.035 / credit' },
];

const TIER_ORDER: Record<BillingTier, number> = { Free: 1, Pro: 2, Premium: 3 };

// Webhook lag: after Checkout returns we poll the summary until the credit
// grant lands (or give up quietly after a minute).
const SUCCESS_POLL_MS = 3000;
const SUCCESS_POLL_TIMEOUT_MS = 60000;

function formatCredits(value: number): string {
  const rounded = Math.round(value * 10) / 10;
  return rounded.toLocaleString(undefined, { maximumFractionDigits: 1 });
}

export function BillingPage() {
  const navigate = useNavigate();
  const { isLoaded, isSignedIn } = useUser();
  const [searchParams, setSearchParams] = useSearchParams();
  const returnStatus = searchParams.get('status'); // 'success' | 'cancelled' | null

  const [pollingForGrant, setPollingForGrant] = useState(returnStatus === 'success');
  const baselineRef = useRef<string | null>(null);

  useEffect(() => {
    if (isLoaded && !isSignedIn) {
      navigate('/');
    }
  }, [isLoaded, isSignedIn, navigate]);

  const {
    data: summary,
    isLoading,
    isError,
  } = useQuery({
    queryKey: ['billingSummary'],
    queryFn: billingApi.getSummary,
    enabled: !!isSignedIn,
    refetchInterval: pollingForGrant ? SUCCESS_POLL_MS : false,
  });

  // Stop polling once the summary changes from the first snapshot we saw —
  // that's the webhook landing. If it never changes (webhook beat us back,
  // or is slow), the timeout below ends the poll quietly.
  useEffect(() => {
    if (!pollingForGrant || !summary) return;
    const snapshot = JSON.stringify(summary);
    if (baselineRef.current === null) {
      baselineRef.current = snapshot;
      return;
    }
    if (snapshot !== baselineRef.current) {
      setPollingForGrant(false);
      toast({ title: 'Purchase applied', description: 'Your account has been updated.' });
    }
  }, [summary, pollingForGrant]);

  useEffect(() => {
    if (!pollingForGrant) return;
    const timeout = setTimeout(() => setPollingForGrant(false), SUCCESS_POLL_TIMEOUT_MS);
    return () => clearTimeout(timeout);
  }, [pollingForGrant]);

  const checkoutMutation = useMutation({
    mutationFn: ({ kind, id }: { kind: CheckoutKind; id: CheckoutItemId }) =>
      billingApi.createCheckoutSession(kind, id),
    onSuccess: url => window.location.assign(url),
    onError: (e: Error) =>
      toast({ title: 'Checkout failed', description: e.message, variant: 'destructive' }),
  });

  const portalMutation = useMutation({
    mutationFn: billingApi.createPortalSession,
    onSuccess: url => window.location.assign(url),
    onError: (e: Error) =>
      toast({ title: 'Billing portal unavailable', description: e.message, variant: 'destructive' }),
  });

  const busy = checkoutMutation.isPending || portalMutation.isPending;
  const hasSubscription =
    summary?.subscriptionStatus === 'active' || summary?.subscriptionStatus === 'past_due';

  const dismissReturnBanner = () => {
    searchParams.delete('status');
    setSearchParams(searchParams, { replace: true });
    setPollingForGrant(false);
  };

  return (
    <div className="min-h-screen bg-background p-4 md:p-8 pt-20 md:pt-8">
      <div className="max-w-5xl mx-auto space-y-6">
        <div className="border-b border-border pb-4">
          <h1 className="text-xl font-semibold tracking-tight text-foreground">Billing</h1>
          <p className="text-xs text-muted-foreground mt-1">
            Plan, credits, and purchase history
          </p>
        </div>

        {returnStatus === 'success' && (
          <ReturnBanner tone="success" onDismiss={dismissReturnBanner}>
            {pollingForGrant ? (
              <span className="flex items-center gap-2">
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
                Payment received — applying it to your account…
              </span>
            ) : (
              'Payment received. Your account is up to date.'
            )}
          </ReturnBanner>
        )}
        {returnStatus === 'cancelled' && (
          <ReturnBanner tone="neutral" onDismiss={dismissReturnBanner}>
            Checkout was cancelled — you haven't been charged.
          </ReturnBanner>
        )}
        {summary?.subscriptionStatus === 'past_due' && (
          <div className="rounded-xl border border-amber-500/40 bg-amber-500/10 px-4 py-3 text-sm text-amber-700 dark:text-amber-400">
            Your last payment failed. Update your payment method in the billing portal to keep
            your subscription — credits won't refill until payment succeeds.
          </div>
        )}

        {isError && (
          <div className="rounded-xl bg-destructive/10 border border-destructive/40 text-destructive dark:text-red-400 px-4 py-3 text-sm">
            <span className="font-medium">Error:</span> Failed to load billing details. Try
            refreshing the page.
          </div>
        )}

        <SummaryCard
          summary={summary}
          isLoading={isLoading}
          busy={busy}
          portalPending={portalMutation.isPending}
          onOpenPortal={() => portalMutation.mutate()}
        />

        <section className="space-y-3">
          <h2 className="text-sm font-semibold tracking-tight text-foreground">Plans</h2>
          <div className="grid gap-4 lg:grid-cols-3">
            {TIER_PLANS.map(plan => {
              const isCurrent = summary?.tier === plan.id;
              return (
                <div
                  key={plan.id}
                  className={cn(
                    'relative flex h-full flex-col rounded-xl border bg-card p-5',
                    plan.highlighted && !isCurrent
                      ? 'border-foreground/40'
                      : 'border-border/80',
                    isCurrent && 'ring-1 ring-primary/60',
                  )}
                >
                  {isCurrent && (
                    <span className="absolute -top-2.5 left-4 rounded-full bg-primary px-2.5 py-0.5 text-[10px] font-semibold uppercase tracking-wider text-primary-foreground">
                      Current plan
                    </span>
                  )}
                  <div className="font-mono text-[11px] uppercase tracking-widest text-muted-foreground">
                    {plan.id}
                  </div>
                  <div className="mt-2 flex items-baseline gap-1">
                    <span className="font-mono text-3xl font-semibold tabular-nums text-foreground">
                      {plan.price}
                    </span>
                    {plan.period && (
                      <span className="text-sm text-muted-foreground">{plan.period}</span>
                    )}
                  </div>
                  <p className="mt-1 text-xs text-muted-foreground">{plan.tagline}</p>
                  <ul className="mt-4 flex-1 space-y-2">
                    {plan.features.map(f => (
                      <li key={f} className="flex items-start gap-2 text-sm text-foreground">
                        <Check
                          className="mt-0.5 h-3.5 w-3.5 shrink-0"
                          style={{ color: 'var(--chart-gain)' }}
                        />
                        <span>{f}</span>
                      </li>
                    ))}
                  </ul>
                  <PlanAction
                    plan={plan.id}
                    summary={summary}
                    hasSubscription={hasSubscription}
                    busy={busy}
                    checkoutPending={
                      checkoutMutation.isPending &&
                      checkoutMutation.variables?.id === plan.id
                    }
                    onSubscribe={() =>
                      checkoutMutation.mutate({ kind: 'subscription', id: plan.id as CheckoutItemId })
                    }
                    onOpenPortal={() => portalMutation.mutate()}
                  />
                </div>
              );
            })}
          </div>
        </section>

        <section className="space-y-3">
          <h2 className="text-sm font-semibold tracking-tight text-foreground">Credit packs</h2>
          <p className="text-xs text-muted-foreground">
            One-time top-ups. Purchased credits never expire and are spent after your monthly
            allowance.
          </p>
          <div className="grid gap-4 sm:grid-cols-2 lg:max-w-2xl">
            {PACKS.map(pack => (
              <div
                key={pack.id}
                className="flex items-center justify-between gap-4 rounded-xl border border-border/80 bg-card p-5"
              >
                <div>
                  <div className="text-sm font-semibold text-foreground">
                    {pack.credits.toLocaleString()} credits
                  </div>
                  <div className="mt-0.5 text-xs text-muted-foreground">
                    {pack.name} · {pack.note}
                  </div>
                </div>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={busy || isLoading}
                  onClick={() => checkoutMutation.mutate({ kind: 'pack', id: pack.id })}
                >
                  {checkoutMutation.isPending && checkoutMutation.variables?.id === pack.id ? (
                    <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  ) : (
                    pack.price
                  )}
                </Button>
              </div>
            ))}
          </div>
        </section>

        <p className="text-xs text-muted-foreground">
          Monthly credits reset with each billing cycle and don't roll over; purchased credits
          never expire. Invoices, receipts, card changes, and cancellation are handled in the
          billing portal.
        </p>
      </div>
    </div>
  );
}

function ReturnBanner({
  tone,
  onDismiss,
  children,
}: {
  tone: 'success' | 'neutral';
  onDismiss: () => void;
  children: React.ReactNode;
}) {
  return (
    <div
      className={cn(
        'relative rounded-xl border px-4 py-3 text-sm',
        tone === 'success'
          ? 'border-emerald-500/40 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400'
          : 'border-border bg-card text-muted-foreground',
      )}
    >
      {children}
      <button
        onClick={onDismiss}
        className="absolute top-2.5 right-2.5 rounded-md p-0.5 opacity-70 transition-opacity hover:opacity-100"
        aria-label="Dismiss"
      >
        <X className="h-3.5 w-3.5" />
      </button>
    </div>
  );
}

function SummaryCard({
  summary,
  isLoading,
  busy,
  portalPending,
  onOpenPortal,
}: {
  summary: BillingSummary | undefined;
  isLoading: boolean;
  busy: boolean;
  portalPending: boolean;
  onOpenPortal: () => void;
}) {
  if (isLoading || !summary) {
    return (
      <div className="animate-pulse rounded-xl border border-border/80 bg-card p-5">
        <div className="h-4 w-24 rounded bg-muted/60" />
        <div className="mt-3 h-8 w-48 rounded bg-muted/40" />
        <div className="mt-3 h-2 w-full rounded bg-muted/40" />
      </div>
    );
  }

  const hasAllowance = summary.maxCredits > 0;
  const used = hasAllowance
    ? Math.min(Math.max(summary.maxCredits - summary.credits, 0), summary.maxCredits)
    : 0;
  const usedPct = hasAllowance ? (used / summary.maxCredits) * 100 : 0;
  const meterColor =
    usedPct >= 95
      ? 'bg-red-600 dark:bg-red-500'
      : usedPct >= 80
      ? 'bg-amber-500'
      : 'bg-primary';

  return (
    <div className="rounded-xl border border-border/80 bg-card p-5">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-2">
            <span className="text-lg font-semibold tracking-tight text-foreground">
              {summary.tier}
            </span>
            <StatusBadge status={summary.subscriptionStatus} />
          </div>
          <p className="mt-0.5 text-xs text-muted-foreground">
            {summary.tier === 'Free'
              ? 'Free plan — monthly credits refill automatically.'
              : 'Credits refill when your monthly payment goes through.'}
          </p>
        </div>
        {summary.hasBillingAccount ? (
          <Button variant="outline" size="sm" disabled={busy} onClick={onOpenPortal}>
            {portalPending ? (
              <Loader2 className="h-3.5 w-3.5 animate-spin" />
            ) : (
              <CreditCard className="h-3.5 w-3.5" />
            )}
            Manage billing
            <ExternalLink className="h-3 w-3 opacity-60" />
          </Button>
        ) : (
          // Portal-session 400s until a Stripe customer exists (first checkout creates one).
          <p className="max-w-[200px] text-right text-[11px] text-muted-foreground">
            Receipts and card management appear here after your first purchase.
          </p>
        )}
      </div>

      <div className="mt-5 grid gap-4 sm:grid-cols-2">
        <div>
          <div className="flex items-baseline justify-between gap-3 mb-0.5">
            <div className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground">
              Monthly credits
            </div>
            <div className="text-xs font-semibold tabular-nums text-foreground">
              {hasAllowance
                ? `${formatCredits(summary.credits)} / ${formatCredits(summary.maxCredits)} left`
                : `${formatCredits(summary.credits)} left`}
            </div>
          </div>
          {hasAllowance && (
            <div
              role="progressbar"
              aria-label="Monthly credits used"
              aria-valuemin={0}
              aria-valuemax={Math.round(summary.maxCredits)}
              aria-valuenow={Math.round(used)}
              className="mt-1.5 h-1.5 w-full rounded-full bg-muted overflow-hidden"
            >
              <div
                className={`h-full rounded-full transition-all ${meterColor}`}
                style={{ width: `${Math.min(usedPct, 100)}%` }}
              />
            </div>
          )}
          <p className="mt-1.5 text-[11px] text-muted-foreground">Resets each billing cycle.</p>
        </div>
        <div>
          <div className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground mb-0.5">
            Purchased credits
          </div>
          <div className="text-lg font-semibold tabular-nums leading-tight text-foreground">
            {formatCredits(summary.purchasedCredits)}
          </div>
          <p className="mt-1.5 text-[11px] text-muted-foreground">
            Never expire — spent after monthly credits run out.
          </p>
        </div>
      </div>
    </div>
  );
}

function StatusBadge({ status }: { status: BillingSummary['subscriptionStatus'] }) {
  if (status === 'active') {
    return (
      <span className="rounded-full bg-emerald-500/10 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wider text-emerald-600 dark:text-emerald-400">
        Active
      </span>
    );
  }
  if (status === 'past_due') {
    return (
      <span className="rounded-full bg-amber-500/10 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wider text-amber-600 dark:text-amber-400">
        Past due
      </span>
    );
  }
  if (status === 'canceled') {
    return (
      <span className="rounded-full bg-muted px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">
        Canceled
      </span>
    );
  }
  return null;
}

function PlanAction({
  plan,
  summary,
  hasSubscription,
  busy,
  checkoutPending,
  onSubscribe,
  onOpenPortal,
}: {
  plan: BillingTier;
  summary: BillingSummary | undefined;
  hasSubscription: boolean;
  busy: boolean;
  checkoutPending: boolean;
  onSubscribe: () => void;
  onOpenPortal: () => void;
}) {
  const isCurrent = summary?.tier === plan;

  if (!summary) {
    return (
      <Button className="mt-5" variant="outline" disabled>
        …
      </Button>
    );
  }

  if (isCurrent) {
    return (
      <Button className="mt-5" variant="outline" disabled>
        Current plan
      </Button>
    );
  }

  if (plan === 'Free') {
    // Downgrading to Free = cancelling the subscription, which happens in the portal.
    return hasSubscription ? (
      <Button className="mt-5" variant="ghost" disabled={busy} onClick={onOpenPortal}>
        Cancel in portal
      </Button>
    ) : (
      <Button className="mt-5" variant="outline" disabled>
        Included
      </Button>
    );
  }

  // Existing subscribers change plans through the portal (checkout would be
  // rejected by the API); everyone else goes straight to Stripe Checkout.
  if (hasSubscription) {
    return (
      <Button className="mt-5" variant="outline" disabled={busy} onClick={onOpenPortal}>
        {TIER_ORDER[plan] > TIER_ORDER[summary.tier] ? 'Upgrade in portal' : 'Downgrade in portal'}
      </Button>
    );
  }

  return (
    <Button className="mt-5" disabled={busy} onClick={onSubscribe}>
      {checkoutPending ? <Loader2 className="h-4 w-4 animate-spin" /> : `Subscribe to ${plan}`}
    </Button>
  );
}
