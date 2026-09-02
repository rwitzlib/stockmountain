import { useEffect, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { ExternalLink, Loader2 } from 'lucide-react';
import { Button } from '../ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../ui/dialog';
import {
  billingApi,
  BillingInterval,
  BillingTier,
  PlanChangePreview,
  PlanChangeResult,
  PlanId,
} from '../../api/billingApi';
import { formatDate, formatMoney } from '../../utils/billingFormat';

export interface PlanChangeTarget {
  id: PlanId;
  tier: BillingTier;
  interval: BillingInterval;
  /** Human label for the title, e.g. "Premium plan (annual)". */
  label: string;
  /** What the customer gains, e.g. "Your monthly allowance rises to 5,000 credits right away." */
  benefit?: string;
}

interface PlanChangeModalProps {
  target: PlanChangeTarget | null;
  onClose: () => void;
  /** The API accepted the change (applied, scheduled, or awaiting a payment step). */
  onResult: (result: PlanChangeResult) => void;
}

/**
 * Confirm dialog for an existing subscriber's plan change. Upgrades (and monthly→annual
 * switches) are charged today, prorated, so the preview shows the real amount before
 * the customer commits; downgrades are scheduled for the end of the paid period and
 * cost nothing now. Stays open after a change that needs a Stripe payment step so the
 * link is a user click (popup blockers swallow window.open from async callbacks).
 */
export function PlanChangeModal({ target, onClose, onResult }: PlanChangeModalProps) {
  const [paymentUrl, setPaymentUrl] = useState<string | null>(null);

  // Reset the payment step whenever a different plan is opened.
  useEffect(() => {
    setPaymentUrl(null);
  }, [target?.id]);

  const preview = useQuery({
    queryKey: ['planChangePreview', target?.id],
    queryFn: () => billingApi.previewPlanChange(target!.id),
    enabled: !!target,
    staleTime: 0,
    retry: false,
  });

  const change = useMutation({
    mutationFn: (id: PlanId) => billingApi.changePlan(id),
    onSuccess: result => {
      if (result.status === 'requires_action' && result.paymentUrl) {
        setPaymentUrl(result.paymentUrl);
      }
      onResult(result);
    },
  });

  const busy = change.isPending;

  return (
    <Dialog open={!!target} onOpenChange={open => !open && !busy && onClose()}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle className="text-base">
            {target ? `Switch to ${target.label}` : 'Change plan'}
          </DialogTitle>
          <DialogDescription>
            {paymentUrl
              ? 'One more step to finish your upgrade.'
              : 'Review the change before confirming.'}
          </DialogDescription>
        </DialogHeader>

        {paymentUrl ? (
          <PaymentStep url={paymentUrl} />
        ) : preview.isPending ? (
          <div className="flex items-center gap-2 py-4 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" />
            Calculating what changes…
          </div>
        ) : preview.isError ? (
          <p className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive dark:text-red-400">
            {preview.error.message}
          </p>
        ) : preview.data && target ? (
          <PreviewBody preview={preview.data} target={target} />
        ) : null}

        {change.isError && (
          <p className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive dark:text-red-400">
            {change.error.message}
          </p>
        )}

        <DialogFooter className="gap-2 sm:gap-0">
          <Button variant="outline" disabled={busy} onClick={onClose}>
            {paymentUrl ? 'Close' : 'Cancel'}
          </Button>
          {!paymentUrl && preview.data && target && (
            <Button disabled={busy} onClick={() => change.mutate(target.id)}>
              {busy && <Loader2 className="h-3.5 w-3.5 animate-spin" />}
              {preview.data.timing === 'immediate'
                ? `Confirm — pay ${formatMoney(preview.data.amountDueCents, preview.data.currency)}`
                : 'Schedule change'}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function PreviewBody({ preview, target }: { preview: PlanChangePreview; target: PlanChangeTarget }) {
  if (preview.timing === 'immediate') {
    return (
      <div className="space-y-3 text-sm text-foreground">
        <div className="flex items-baseline justify-between rounded-lg border border-border bg-muted/40 px-4 py-3">
          <span className="text-muted-foreground">Due today</span>
          <span className="font-mono text-lg font-semibold tabular-nums">
            {formatMoney(preview.amountDueCents, preview.currency)}
          </span>
        </div>
        <p className="text-muted-foreground">
          That's the prorated difference for the rest of your current billing period, charged
          to your card on file. Your next renewal is billed at the {target.label} price.
        </p>
        {target.benefit && <p>{target.benefit}</p>}
      </div>
    );
  }

  return (
    <div className="space-y-3 text-sm text-foreground">
      <div className="flex items-baseline justify-between rounded-lg border border-border bg-muted/40 px-4 py-3">
        <span className="text-muted-foreground">Takes effect</span>
        <span className="font-semibold">{formatDate(preview.effectiveAt)}</span>
      </div>
      <p className="text-muted-foreground">
        You keep everything in your current plan until then, and nothing is charged today.
        You can undo this any time before it takes effect.
      </p>
      {target.benefit && <p>{target.benefit}</p>}
    </div>
  );
}

function PaymentStep({ url }: { url: string }) {
  return (
    <div className="space-y-3 text-sm">
      <p className="text-muted-foreground">
        Your bank needs to confirm the prorated payment before the upgrade applies. Finish it
        on Stripe's secure invoice page — your plan switches automatically once it's paid.
      </p>
      <Button asChild>
        <a href={url} target="_blank" rel="noopener noreferrer">
          Complete payment
          <ExternalLink className="h-3.5 w-3.5 opacity-70" />
        </a>
      </Button>
    </div>
  );
}
