import { Link, Outlet, useLocation } from 'react-router-dom';
import { Database, Camera, Scan, LucideIcon } from 'lucide-react';
import { useIsAdmin } from '../../hooks/useIsAdmin';

const tools: {
  to: string;
  icon: LucideIcon;
  title: string;
  description: string;
  adminOnly?: boolean;
}[] = [
  {
    to: '/tools/chart-filters',
    icon: Scan,
    title: 'Chart Filters',
    description: 'Configure filters and highlight matches on chart',
  },
  {
    to: '/tools/aggregate',
    icon: Database,
    title: 'Data Aggregator',
    description: 'Upload, visualize, and analyze market data',
    adminOnly: true,
  },
  {
    to: '/tools/snapshot',
    icon: Camera,
    title: 'Snapshot Tool',
    description: 'Input JSON data and view in formatted tables',
    adminOnly: true,
  },
];

export function ToolsPage() {
  const location = useLocation();
  const { isAdmin } = useIsAdmin();
  const isMainToolsPage = location.pathname === '/tools';

  if (!isMainToolsPage) {
    return <Outlet />;
  }

  const visibleTools = tools.filter((tool) => !tool.adminOnly || isAdmin);

  return (
    <div className="min-h-screen bg-background p-4 md:p-8 pt-20 md:pt-8">
      <div className="mx-auto max-w-7xl space-y-6">
        <div className="border-b border-border pb-4">
          <div className="mb-1 text-[11px] font-medium uppercase tracking-widest text-muted-foreground">
            Tools
          </div>
          <h1 className="text-xl font-semibold tracking-tight text-foreground md:text-2xl">
            Trading tools
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">Specialized market analysis utilities.</p>
        </div>

        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
          {visibleTools.map(({ to, icon: Icon, title, description }) => (
            <Link
              key={title}
              to={to}
              className="group block rounded-xl border border-border/80 bg-card p-6 transition-colors hover:border-muted-foreground/40 hover:bg-accent/40"
            >
              <div className="mb-3 flex items-center gap-4">
                <div className="rounded-lg border border-border bg-muted/60 p-2 text-muted-foreground transition-colors group-hover:text-foreground">
                  <Icon className="h-5 w-5" />
                </div>
                <h2 className="text-base font-semibold tracking-tight text-foreground">{title}</h2>
              </div>
              <p className="text-sm text-muted-foreground">{description}</p>
            </Link>
          ))}
        </div>
      </div>
    </div>
  );
}
