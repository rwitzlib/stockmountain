import { NavLink, Outlet } from 'react-router-dom';
import { Globe, Lock } from 'lucide-react';
import { useUser } from '@clerk/react';

export function OptimusPage() {
  const { isSignedIn } = useUser();
  const userIsAuthenticated = !!isSignedIn;

  return (
    <div className="min-h-screen bg-background p-4 md:p-8 pt-20 md:pt-8">
      <div className="mb-6 border-b border-border pb-4">
        <div className="mb-1 text-[11px] font-medium uppercase tracking-widest text-muted-foreground">
          Optimus
        </div>
        <h1 className="text-xl font-semibold tracking-tight text-foreground md:text-2xl">
          Trading system
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">Automated strategy execution platform.</p>
      </div>
      <div className="mb-4 grid grid-cols-1 gap-4 md:grid-cols-2">
        <NavLink
          to="/optimus/public-dashboard"
          className="group block rounded-xl border border-border/80 bg-card p-6 transition-colors hover:border-muted-foreground/40 hover:bg-accent/40"
        >
          <div className="mb-3 flex items-center gap-4">
            <div className="rounded-lg border border-border bg-muted/60 p-2 text-muted-foreground transition-colors group-hover:text-foreground">
              <Globe className="h-5 w-5" />
            </div>
            <h2 className="text-base font-semibold tracking-tight text-foreground">Public Dashboard</h2>
          </div>
          <p className="text-sm text-muted-foreground">
            Browse trading strategies shared by the community
          </p>
        </NavLink>
        {userIsAuthenticated && (
          <NavLink
            to="/optimus/dashboard"
            className="group block rounded-xl border border-border/80 bg-card p-6 transition-colors hover:border-muted-foreground/40 hover:bg-accent/40"
          >
            <div className="mb-3 flex items-center gap-4">
              <div className="rounded-lg border border-border bg-muted/60 p-2 text-muted-foreground transition-colors group-hover:text-foreground">
                <Lock className="h-5 w-5" />
              </div>
              <h2 className="text-base font-semibold tracking-tight text-foreground">My Dashboard</h2>
            </div>
            <p className="text-sm text-muted-foreground">
              View and manage your personal trading strategies
            </p>
          </NavLink>
        )}
      </div>
      <Outlet />
    </div>
  );
}
