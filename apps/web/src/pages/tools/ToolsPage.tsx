import { Link, Outlet, useLocation } from 'react-router-dom';
import { BarChart3, Calculator, Database, Camera, Scan } from 'lucide-react';

export function ToolsPage() {
  const location = useLocation();
  const isMainToolsPage = location.pathname === '/tools';

  if (!isMainToolsPage) {
    return <Outlet />;
  }

  return (
    <div className="min-h-screen bg-background p-4 md:p-8 pt-20 md:pt-8">
      <div className="max-w-7xl mx-auto space-y-6">
        <div className="border-b border-border pb-4">
          <h1 className="text-xl font-mono font-bold uppercase tracking-wider text-foreground"># Trading Tools</h1>
          <p className="text-xs font-mono text-muted-foreground mt-1">{'>> '}Specialized market analysis utilities</p>
        </div>
        
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <Link 
            to="/tools/aggregate"
            className="relative block p-6 bg-card border border-border hover:border-blue-500 dark:hover:border-blue-700 hover:shadow-lg hover:shadow-blue-500/20 dark:hover:shadow-blue-900/20 transition-all duration-200 hover:-translate-y-[2px] group"
          >
            <div className="absolute inset-y-0 left-0 w-1 bg-blue-500 dark:bg-blue-700 group-hover:bg-blue-400 dark:group-hover:bg-blue-500 transition-colors" />
            <div className="flex items-center gap-4 mb-3">
              <div className="p-2 bg-blue-100 dark:bg-blue-950/50 border border-blue-300 dark:border-blue-800 group-hover:border-blue-400 dark:group-hover:border-blue-600 transition-colors">
                <Database className="w-5 h-5 text-blue-600 dark:text-blue-400 group-hover:animate-pulse" />
              </div>
              <h2 className="text-base font-mono font-bold uppercase tracking-wider text-foreground">Data Aggregator</h2>
            </div>
            <p className="text-xs font-mono text-muted-foreground pl-11">
              {'>> '}Upload, visualize, and analyze market data
            </p>
          </Link>

          <Link 
            to="/tools/snapshot"
            className="relative block p-6 bg-card border border-border hover:border-amber-500 dark:hover:border-amber-700 hover:shadow-lg hover:shadow-amber-500/20 dark:hover:shadow-amber-900/20 transition-all duration-200 hover:-translate-y-[2px] group"
          >
            <div className="absolute inset-y-0 left-0 w-1 bg-amber-500 dark:bg-amber-700 group-hover:bg-amber-400 dark:group-hover:bg-amber-500 transition-colors" />
            <div className="flex items-center gap-4 mb-3">
              <div className="p-2 bg-amber-100 dark:bg-amber-950/50 border border-amber-300 dark:border-amber-800 group-hover:border-amber-400 dark:group-hover:border-amber-600 transition-colors">
                <Camera className="w-5 h-5 text-amber-600 dark:text-amber-400 group-hover:animate-pulse" />
              </div>
              <h2 className="text-base font-mono font-bold uppercase tracking-wider text-foreground">Snapshot Tool</h2>
            </div>
            <p className="text-xs font-mono text-muted-foreground pl-11">
              {'>> '}Input JSON data and view in formatted tables
            </p>
          </Link>

          <Link
            to="/tools/chart-filters"
            className="relative block p-6 bg-card border border-border hover:border-purple-500 dark:hover:border-purple-700 hover:shadow-lg hover:shadow-purple-500/20 dark:hover:shadow-purple-900/20 transition-all duration-200 hover:-translate-y-[2px] group"
          >
            <div className="absolute inset-y-0 left-0 w-1 bg-purple-500 dark:bg-purple-700 group-hover:bg-purple-400 dark:group-hover:bg-purple-500 transition-colors" />
            <div className="flex items-center gap-4 mb-3">
              <div className="p-2 bg-purple-100 dark:bg-purple-950/50 border border-purple-300 dark:border-purple-800 group-hover:border-purple-400 dark:group-hover:border-purple-600 transition-colors">
                <Scan className="w-5 h-5 text-purple-600 dark:text-purple-400 group-hover:animate-pulse" />
              </div>
              <h2 className="text-base font-mono font-bold uppercase tracking-wider text-foreground">Chart Filters</h2>
            </div>
            <p className="text-xs font-mono text-muted-foreground pl-11">
              {'>> '}Configure filters and highlight matches on chart
            </p>
          </Link>

          <div className="relative block p-6 bg-muted/30 dark:bg-muted/50 border border-border opacity-60">
            <div className="absolute inset-y-0 left-0 w-1 bg-purple-500 dark:bg-purple-700" />
            <div className="flex items-center gap-4 mb-3">
              <div className="p-2 bg-purple-100/50 dark:bg-purple-950/30 border border-purple-300/50 dark:border-purple-800">
                <Calculator className="w-5 h-5 text-purple-500 dark:text-purple-500" />
              </div>
              <h2 className="text-base font-mono font-bold uppercase tracking-wider text-muted-foreground">Position Sizer</h2>
            </div>
            <p className="text-xs font-mono text-muted-foreground/60 pl-11">
              {'>> '}Calculate optimal position sizes [COMING SOON]
            </p>
          </div>

          <div className="relative block p-6 bg-muted/30 dark:bg-muted/50 border border-border opacity-60">
            <div className="absolute inset-y-0 left-0 w-1 bg-green-500 dark:bg-green-700" />
            <div className="flex items-center gap-4 mb-3">
              <div className="p-2 bg-green-100/50 dark:bg-green-950/30 border border-green-300/50 dark:border-green-800">
                <BarChart3 className="w-5 h-5 text-green-500 dark:text-green-500" />
              </div>
              <h2 className="text-base font-mono font-bold uppercase tracking-wider text-muted-foreground">Performance Analyzer</h2>
            </div>
            <p className="text-xs font-mono text-muted-foreground/60 pl-11">
              {'>> '}Analyze trading performance [COMING SOON]
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
