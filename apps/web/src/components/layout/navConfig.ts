import {
  LucideIcon,
  Home,
  CandlestickChart,
  Search,
  BarChart2,
  Activity,
  Wrench,
  LayoutDashboard,
  ArrowLeftRight,
  CalendarDays,
  Globe,
  Database,
  Camera,
  Scan,
} from 'lucide-react';

export interface NavChild {
  path: string;
  icon: LucideIcon;
  label: string;
  /** Match the route exactly (NavLink `end`) instead of by prefix */
  end?: boolean;
  /** Only shown to admins (the route is also gated by AdminGate) */
  adminOnly?: boolean;
}

export interface NavItem extends NavChild {
  children?: NavChild[];
}

// Single source of truth for app navigation (desktop sidebar + mobile menu).
export const navItems: NavItem[] = [
  { path: '/', icon: Home, label: 'Dashboard', end: true },
  { path: '/chart', icon: CandlestickChart, label: 'Charts' },
  { path: '/scanner', icon: Search, label: 'Scanner' },
  { path: '/backtest', icon: BarChart2, label: 'Backtests' },
  {
    path: '/strategies',
    icon: Activity,
    label: 'Strategies',
    children: [
      { path: '/strategies', icon: LayoutDashboard, label: 'Overview', end: true },
      { path: '/strategies/trades', icon: ArrowLeftRight, label: 'Trades' },
      { path: '/strategies/calendar', icon: CalendarDays, label: 'Calendar' },
      { path: '/strategies/community', icon: Globe, label: 'Community' },
    ],
  },
  {
    path: '/tools',
    icon: Wrench,
    label: 'Tools',
    children: [
      { path: '/tools', icon: LayoutDashboard, label: 'Overview', end: true },
      { path: '/tools/chart-filters', icon: Scan, label: 'Chart Filters' },
      { path: '/tools/aggregate', icon: Database, label: 'Data Aggregator', adminOnly: true },
      { path: '/tools/snapshot', icon: Camera, label: 'Snapshot', adminOnly: true },
    ],
  },
];
