import { ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { Lock } from 'lucide-react';
import { useIsAdmin } from '../../hooks/useIsAdmin';
import { Card } from '../ui/card';
import { Button } from '../ui/button';

/** Renders children only for admins; everyone else sees an access notice. */
export function AdminGate({ children }: { children: ReactNode }) {
  const navigate = useNavigate();
  const { isAdmin, isLoading } = useIsAdmin();

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <div className="text-muted-foreground">Loading...</div>
      </div>
    );
  }

  if (!isAdmin) {
    return (
      <div className="min-h-screen bg-background p-4 pt-20 text-foreground md:p-8 md:pt-8">
        <div className="mx-auto max-w-[720px]">
          <Card className="p-8 text-center">
            <Lock className="mx-auto mb-3 h-8 w-8 text-muted-foreground" />
            <h1 className="text-xl font-semibold tracking-tight">Admins only</h1>
            <p className="mx-auto mt-2 max-w-md text-sm text-muted-foreground">
              This tool is restricted to administrators.
            </p>
            <Button className="mt-5" onClick={() => navigate('/tools')}>
              Back to tools
            </Button>
          </Card>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
