import { Link } from 'react-router-dom';
import { LineChart, BarChart3, Wrench, CandlestickChart, Search, Bot, LucideIcon } from 'lucide-react';

const modules: {
  to: string;
  icon: LucideIcon;
  title: string;
  description: string;
  badge?: string;
}[] = [
  {
    to: '/chart',
    icon: CandlestickChart,
    title: 'Stock Charts',
    description: 'Real-time and historical market data visualization',
  },
  {
    to: '/optimus',
    icon: Bot,
    title: 'Optimus',
    description: 'Automated trading strategy execution platform',
  },
  {
    to: '/scanner',
    icon: Search,
    title: 'Stock Scanner',
    description: 'Advanced market screening and opportunity detection',
  },
  {
    to: '/backtest',
    icon: BarChart3,
    title: 'Backtest Analysis',
    description: 'Historical strategy performance evaluation',
  },
  {
    to: '/tools',
    icon: Wrench,
    title: 'Trading Tools',
    description: 'Specialized market analysis utilities',
  },
  {
    to: '/live',
    icon: LineChart,
    title: 'Live Trading',
    description: 'Real-time execution monitoring',
    badge: 'Coming soon',
  },
];

export function HomePage() {
  return (
    <div className="min-h-screen bg-background">
      <div className="mx-auto max-w-7xl px-4 py-12 pt-24 md:pt-12">
        <div className="mb-8 border-b border-border pb-6">
          <div className="mb-1.5 text-[11px] font-medium uppercase tracking-widest text-muted-foreground">
            StockMountain
          </div>
          <h1 className="text-2xl font-semibold tracking-tight text-foreground md:text-3xl">
            Trading analytics platform
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">Select a module to begin.</p>
        </div>

        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
          {modules.map(({ to, icon: Icon, title, description, badge }) => (
            <Link
              key={to}
              to={to}
              className="group block rounded-xl border border-border/80 bg-card p-6 transition-colors hover:border-muted-foreground/40 hover:bg-accent/40"
            >
              <div className="mb-3 flex items-center gap-4">
                <div className="rounded-lg border border-border bg-muted/60 p-2 text-muted-foreground transition-colors group-hover:text-foreground">
                  <Icon className="h-5 w-5" />
                </div>
                <h2 className="text-base font-semibold tracking-tight text-foreground">{title}</h2>
                {badge && (
                  <span className="ml-auto rounded-full border border-border px-2 py-0.5 text-[10px] font-medium uppercase tracking-wider text-muted-foreground">
                    {badge}
                  </span>
                )}
              </div>
              <p className="text-sm text-muted-foreground">{description}</p>
            </Link>
          ))}
        </div>
      </div>
    </div>
  );
}
