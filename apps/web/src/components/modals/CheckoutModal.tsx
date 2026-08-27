import { useCallback } from 'react';
import { loadStripe } from '@stripe/stripe-js';
import { EmbeddedCheckout, EmbeddedCheckoutProvider } from '@stripe/react-stripe-js';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '../ui/dialog';
import { billingApi, CheckoutItemId, CheckoutKind } from '../../api/billingApi';
import { toast } from '../../hooks/use-toast';

// Vite inlines this at build time; when it's unset the billing page disables
// purchase buttons instead of mounting a checkout that can never load.
const PUBLISHABLE_KEY = import.meta.env.VITE_STRIPE_PUBLISHABLE_KEY as string | undefined;

export const isCheckoutConfigured = !!PUBLISHABLE_KEY;

// loadStripe must run once per page, not per render.
const stripePromise = PUBLISHABLE_KEY ? loadStripe(PUBLISHABLE_KEY) : null;

export interface CheckoutItem {
  kind: CheckoutKind;
  id: CheckoutItemId;
  /** Short human label for the dialog title, e.g. "Pro plan" or "250 credits". */
  label: string;
}

interface CheckoutModalProps {
  item: CheckoutItem | null;
  onClose: () => void;
  /** Payment finished inside the embedded checkout; the webhook is now in flight. */
  onComplete: () => void;
}

export function CheckoutModal({ item, onClose, onComplete }: CheckoutModalProps) {
  // A checkout session is single-use: the provider mounts fresh for each item
  // (keyed below), and closing the dialog discards the session entirely.
  const fetchClientSecret = useCallback(async () => {
    if (!item) throw new Error('No checkout item selected');
    try {
      return await billingApi.createCheckoutSession(item.kind, item.id);
    } catch (e) {
      toast({
        title: 'Checkout failed',
        description: e instanceof Error ? e.message : 'Please try again.',
        variant: 'destructive',
      });
      onClose();
      throw e;
    }
  }, [item, onClose]);

  if (!stripePromise) return null;

  return (
    <Dialog open={!!item} onOpenChange={open => !open && onClose()}>
      <DialogContent className="max-w-2xl gap-0 p-0">
        <DialogHeader className="border-b border-border px-5 py-3.5">
          <DialogTitle className="text-sm">
            {item ? `Checkout — ${item.label}` : 'Checkout'}
          </DialogTitle>
        </DialogHeader>
        {/* Stripe's embedded frame brings its own (light) background; the white
            wrapper keeps the seam invisible in dark mode. */}
        <div className="max-h-[80vh] overflow-y-auto rounded-b-lg bg-white">
          {item && (
            <EmbeddedCheckoutProvider
              key={`${item.kind}:${item.id}`}
              stripe={stripePromise}
              options={{ fetchClientSecret, onComplete }}
            >
              <EmbeddedCheckout />
            </EmbeddedCheckoutProvider>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
