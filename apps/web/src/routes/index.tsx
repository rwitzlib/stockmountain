import { RouteObject } from 'react-router-dom';
import { lazy, Suspense } from 'react';
import { HomePage } from '../pages/HomePage';
import { BacktestPage } from '../pages/BacktestPage';
import { BacktestCreatePage } from '../pages/BacktestCreatePage';
import { BacktestDetailPage } from '../pages/BacktestDetailPage';
import { StockChartPage } from '../pages/StockChartPage';
import { ToolsPage } from '../pages/tools/ToolsPage';
import { ScannerPage } from '../pages/ScannerPage';
import { SharedBacktestPage } from '../pages/SharedBacktestPage';
import { FilterDocsPage } from '../pages/docs/FilterDocsPage';
import { BillingPage } from '../pages/BillingPage';

// Lazy load tool pages for better performance
const AggregatePage = lazy(() => import('../pages/tools/aggregate/AggregatePage').then(module => ({ default: module.AggregatePage })));
const SnapshotPage = lazy(() => import('../pages/tools/snapshot/SnapshotPage').then(module => ({ default: module.SnapshotPage })));
const ChartFiltersPage = lazy(() => import('../pages/tools/chart-filters/ChartFiltersPage').then(module => ({ default: module.ChartFiltersPage })));

// Loading fallback component
const LoadingFallback = () => (
  <div className="flex items-center justify-center min-h-screen">
    <div className="text-gray-400">Loading...</div>
  </div>
);

export const routes: RouteObject[] = [
  {
    path: '/',
    element: <HomePage />,
  },
  {
    path: '/backtest',
    element: <BacktestPage />,
  },
  {
    path: '/backtest/create',
    element: <BacktestCreatePage />,
  },
  {
    path: '/backtest/:id',
    element: <BacktestDetailPage />,
  },
  {
    // Public share view — must render with no auth (viewers may have no account).
    path: '/share/:shareId',
    element: <SharedBacktestPage />,
  },
  {
    // Public filter DSL reference, rendered from docs/filters/*.md (plan 15). Linked from
    // /filters/functions docsUrl and the filter builder's autocomplete.
    path: '/docs/filters',
    element: <FilterDocsPage />,
  },
  {
    path: '/docs/filters/:name',
    element: <FilterDocsPage />,
  },
  {
    path: '/chart',
    element: <StockChartPage />,
  },
  {
    path: '/tools',
    element: <ToolsPage />,
    children: [
      {
        path: 'aggregate',
        element: (
          <Suspense fallback={<LoadingFallback />}>
            <AggregatePage />
          </Suspense>
        ),
      },
      {
        path: 'snapshot',
        element: (
          <Suspense fallback={<LoadingFallback />}>
            <SnapshotPage />
          </Suspense>
        ),
      },
      {
        path: 'chart-filters',
        element: (
          <Suspense fallback={<LoadingFallback />}>
            <ChartFiltersPage />
          </Suspense>
        ),
      },
    ],
  },
  {
    path: '/scanner',
    element: <ScannerPage />,
  },
  {
    path: '/settings',
    element: <div className="p-6">Settings Page</div>,
  },
  {
    path: '/billing',
    element: <BillingPage />,
  },
  {
    path: '*',
    element: <div className="p-6">404 - Page Not Found</div>,
  }
];
