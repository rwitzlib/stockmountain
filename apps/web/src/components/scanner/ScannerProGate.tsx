import { ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { useUser } from '@clerk/react';
import { Lock } from 'lucide-react';
import { billingApi } from '../../api/billingApi';
import { Card } from '../ui/card';
import { Button } from '../ui/button';

/**
 * Scanner surfaces are Pro-gated. This renders an upgrade panel for Free users;
 * the API's [RequiresTier(Pro)] on /scanner and /scan is the enforcement backstop.
 */
export function ScannerProGate({ children }: { children: ReactNode }) {
  const navigate = useNavigate();
  const { user } = useUser();

  // Keyed by user (shared with BillingPage) so a sign-out/sign-in switch can
  // never gate on another user's cached tier.
  const { data: summary } = useQuery({
    queryKey: ['billingSummary', user?.id],
    queryFn: billingApi.getSummary,
    enabled: !!user?.id,
  });

  if (summary?.tier === 'Free') {
    return (
      <div className="min-h-screen bg-background p-4 pt-20 text-foreground md:p-8 md:pt-8">
        <div className="mx-auto max-w-[720px]">
          <Card className="p-8 text-center">
            <Lock className="mx-auto mb-3 h-8 w-8 text-muted-foreground" />
            <h1 className="text-xl font-semibold tracking-tight">The scanner is a Pro feature</h1>
            <p className="mx-auto mt-2 max-w-md text-sm text-muted-foreground">
              Build saved scanners with the same filter language as your strategies and
              backtests, and run them against live market data.
            </p>
            <Button className="mt-5" onClick={() => navigate('/billing')}>
              Upgrade to Pro
            </Button>
          </Card>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
