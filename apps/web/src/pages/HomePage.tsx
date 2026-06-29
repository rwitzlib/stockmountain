import { Link } from 'react-router-dom';
import { LineChart, BarChart3, Wrench, CandlestickChart, Search, Bot } from 'lucide-react';

export function HomePage() {
  return (
    <div className="bg-background min-h-screen">
      <div className="max-w-7xl mx-auto px-4 py-12 pt-24">
        <div className="border-b border-border pb-6 mb-8">
          <h1 className="text-2xl font-mono font-bold uppercase tracking-wider text-foreground mb-2"># Trading Analytics Platform</h1>
          <p className="text-xs font-mono text-muted-foreground">{'>> '}Select a module to begin</p>
        </div>
        
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <Link 
            to="/chart"
            className="relative block p-6 bg-card border border-border hover:border-green-500 dark:hover:border-green-700 hover:shadow-lg hover:shadow-green-500/20 dark:hover:shadow-green-900/20 transition-all duration-200 hover:-translate-y-[2px] group"
          >
            <div className="absolute inset-y-0 left-0 w-1 bg-green-500 dark:bg-green-700 group-hover:bg-green-400 dark:group-hover:bg-green-500 transition-colors" />
            <div className="flex items-center gap-4 mb-3">
              <div className="p-2 bg-green-100 dark:bg-green-950/50 border border-green-300 dark:border-green-800 group-hover:border-green-400 dark:group-hover:border-green-600 transition-colors">
                <CandlestickChart className="w-5 h-5 text-green-600 dark:text-green-400 group-hover:animate-pulse" />
              </div>
              <h2 className="text-base font-mono font-bold uppercase tracking-wider text-foreground">Stock Charts</h2>
            </div>
            <p className="text-xs font-mono text-muted-foreground pl-11">
              {'>> '}Real-time and historical market data visualization
            </p>
          </Link>

          <Link 
            to="/optimus" 
            className="relative block p-6 bg-card border border-border hover:border-primary dark:hover:border-cyan-700 hover:shadow-lg hover:shadow-primary/20 dark:hover:shadow-cyan-900/20 transition-all duration-200 hover:-translate-y-[2px] group"
          >
            <div className="absolute inset-y-0 left-0 w-1 bg-primary dark:bg-cyan-700 group-hover:bg-primary/80 dark:group-hover:bg-cyan-500 transition-colors" />
            <div className="flex items-center gap-4 mb-3">
              <div className="p-2 bg-primary/10 dark:bg-cyan-950/50 border border-primary/30 dark:border-cyan-800 group-hover:border-primary/50 dark:group-hover:border-cyan-600 transition-colors">
                <Bot className="w-5 h-5 text-primary dark:text-cyan-400 group-hover:animate-pulse" />
              </div>
              <h2 className="text-base font-mono font-bold uppercase tracking-wider text-foreground">Optimus</h2>
            </div>
            <p className="text-xs font-mono text-muted-foreground pl-11">
              {'>> '}Automated trading strategy execution platform
            </p>
          </Link>

          <Link 
            to="/scanner" 
            className="relative block p-6 bg-card border border-border hover:border-indigo-500 dark:hover:border-indigo-700 hover:shadow-lg hover:shadow-indigo-500/20 dark:hover:shadow-indigo-900/20 transition-all duration-200 hover:-translate-y-[2px] group"
          >
            <div className="absolute inset-y-0 left-0 w-1 bg-indigo-500 dark:bg-indigo-700 group-hover:bg-indigo-400 dark:group-hover:bg-indigo-500 transition-colors" />
            <div className="flex items-center gap-4 mb-3">
              <div className="p-2 bg-indigo-100 dark:bg-indigo-950/50 border border-indigo-300 dark:border-indigo-800 group-hover:border-indigo-400 dark:group-hover:border-indigo-600 transition-colors">
                <Search className="w-5 h-5 text-indigo-600 dark:text-indigo-400 group-hover:animate-pulse" />
              </div>
              <h2 className="text-base font-mono font-bold uppercase tracking-wider text-foreground">Stock Scanner</h2>
            </div>
            <p className="text-xs font-mono text-muted-foreground pl-11">
              {'>> '}Advanced market screening and opportunity detection
            </p>
          </Link>

          <Link 
            to="/backtest" 
            className="relative block p-6 bg-card border border-border hover:border-blue-500 dark:hover:border-blue-700 hover:shadow-lg hover:shadow-blue-500/20 dark:hover:shadow-blue-900/20 transition-all duration-200 hover:-translate-y-[2px] group"
          >
            <div className="absolute inset-y-0 left-0 w-1 bg-blue-500 dark:bg-blue-700 group-hover:bg-blue-400 dark:group-hover:bg-blue-500 transition-colors" />
            <div className="flex items-center gap-4 mb-3">
              <div className="p-2 bg-blue-100 dark:bg-blue-950/50 border border-blue-300 dark:border-blue-800 group-hover:border-blue-400 dark:group-hover:border-blue-600 transition-colors">
                <BarChart3 className="w-5 h-5 text-blue-600 dark:text-blue-400 group-hover:animate-pulse" />
              </div>
              <h2 className="text-base font-mono font-bold uppercase tracking-wider text-foreground">Backtest Analysis</h2>
            </div>
            <p className="text-xs font-mono text-muted-foreground pl-11">
              {'>> '}Historical strategy performance evaluation
            </p>
          </Link>
          
          <Link 
            to="/tools"
            className="relative block p-6 bg-card border border-border hover:border-purple-500 dark:hover:border-purple-700 hover:shadow-lg hover:shadow-purple-500/20 dark:hover:shadow-purple-900/20 transition-all duration-200 hover:-translate-y-[2px] group"
          >
            <div className="absolute inset-y-0 left-0 w-1 bg-purple-500 dark:bg-purple-700 group-hover:bg-purple-400 dark:group-hover:bg-purple-500 transition-colors" />
            <div className="flex items-center gap-4 mb-3">
              <div className="p-2 bg-purple-100 dark:bg-purple-950/50 border border-purple-300 dark:border-purple-800 group-hover:border-purple-400 dark:group-hover:border-purple-600 transition-colors">
                <Wrench className="w-5 h-5 text-purple-600 dark:text-purple-400 group-hover:animate-pulse" />
              </div>
              <h2 className="text-base font-mono font-bold uppercase tracking-wider text-foreground">Trading Tools</h2>
            </div>
            <p className="text-xs font-mono text-muted-foreground pl-11">
              {'>> '}Specialized market analysis utilities
            </p>
          </Link>

          <Link 
            to="/live"
            className="relative block p-6 bg-card border border-border hover:border-orange-500 dark:hover:border-orange-700 hover:shadow-lg hover:shadow-orange-500/20 dark:hover:shadow-orange-900/20 transition-all duration-200 hover:-translate-y-[2px] group"
          >
            <div className="absolute inset-y-0 left-0 w-1 bg-orange-500 dark:bg-orange-700 group-hover:bg-orange-400 dark:group-hover:bg-orange-500 transition-colors" />
            <div className="flex items-center gap-4 mb-3">
              <div className="p-2 bg-orange-100 dark:bg-orange-950/50 border border-orange-300 dark:border-orange-800 group-hover:border-orange-400 dark:group-hover:border-orange-600 transition-colors">
                <LineChart className="w-5 h-5 text-orange-600 dark:text-orange-400 group-hover:animate-pulse" />
              </div>
              <h2 className="text-base font-mono font-bold uppercase tracking-wider text-foreground">Live Trading</h2>
            </div>
            <p className="text-xs font-mono text-muted-foreground pl-11">
              {'>> '}Real-time execution monitoring [COMING SOON]
            </p>
          </Link>
        </div>
      </div>
    </div>
  );
}