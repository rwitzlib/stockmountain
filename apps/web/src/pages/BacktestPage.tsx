import { useEffect, useState, useMemo, useCallback, useRef } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { BacktestResultsTable } from '../components/tables/BacktestResultsTable';
import { BacktestStatistics } from '../components/backtest/BacktestStatistics';
import { BacktestInsights } from '../components/backtest/BacktestInsights';
import { BacktestEntry } from '../types/backtest';
import { backtestApi } from '../api/backtestApi';
import { Clock } from '../components/clock/Clock';
import { MarketStatus } from '../components/market';
import { ApiStatus } from '../components/status';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';
import { Search, Filter, X, ChevronDown, ChevronRight, RefreshCw, BarChart3 } from 'lucide-react';

type StatusFilter = 'All' | 'Completed' | 'InProgress' | 'Failed';

const POLL_IDLE_MS = 30000;
const POLL_ACTIVE_MS = 5000;

function formatLastRefreshed(date: Date | null): string {
  if (!date) return 'Never';
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
}

export function BacktestPage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const [error, setError] = useState('');
  const [backtestResults, setBacktestResults] = useState<BacktestEntry[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [lastRefreshed, setLastRefreshed] = useState<Date | null>(null);
  const [showInsights, setShowInsights] = useState(false);
  const [sortConfig, setSortConfig] = useState<{
    key: keyof BacktestEntry | null;
    direction: 'asc' | 'desc';
  }>({
    key: 'createdAt',
    direction: 'desc'
  });
  const hasLoadedOnce = useRef(false);

  // Filter state from URL params
  const statusFilter = (searchParams.get('status') as StatusFilter) || 'All';
  const searchQuery = searchParams.get('search') || '';
  const minProfit = searchParams.get('minProfit') ? parseFloat(searchParams.get('minProfit')!) : null;
  const maxProfit = searchParams.get('maxProfit') ? parseFloat(searchParams.get('maxProfit')!) : null;
  const startDate = searchParams.get('startDate') || '';
  const endDate = searchParams.get('endDate') || '';

  const hasInProgress = useMemo(
    () => backtestResults.some(r => r.status === 'InProgress'),
    [backtestResults]
  );

  const fetchBacktestList = useCallback(async () => {
    if (hasLoadedOnce.current) {
      setIsRefreshing(true);
    }
    try {
      const apiResults = await backtestApi.getBacktests();
      setBacktestResults(apiResults);
      setLastRefreshed(new Date());
      setError('');
    } catch (e) {
      console.error('Failed to fetch backtest list:', e);
      setError('Failed to fetch backtest data');
    } finally {
      hasLoadedOnce.current = true;
      setIsLoading(false);
      setIsRefreshing(false);
    }
  }, []);

  // Initial fetch + adaptive polling (faster while any backtest is InProgress)
  useEffect(() => {
    fetchBacktestList();
  }, [fetchBacktestList]);

  useEffect(() => {
    const intervalMs = hasInProgress ? POLL_ACTIVE_MS : POLL_IDLE_MS;
    const interval = setInterval(fetchBacktestList, intervalMs);
    return () => clearInterval(interval);
  }, [fetchBacktestList, hasInProgress]);

  const updateFilter = (key: string, value: string) => {
    const newParams = new URLSearchParams(searchParams);
    if (value === '' || value === 'All') {
      newParams.delete(key);
    } else {
      newParams.set(key, value);
    }
    setSearchParams(newParams);
  };

  const clearFilters = () => {
    setSearchParams({});
  };

  const sortData = (key: keyof BacktestEntry) => {
    const direction = sortConfig.key === key && sortConfig.direction === 'asc' ? 'desc' : 'asc';
    setSortConfig({ key, direction });
  };

  const filteredAndSortedData = useMemo((): BacktestEntry[] => {
    let filtered = [...backtestResults];

    if (statusFilter !== 'All') {
      filtered = filtered.filter(result => {
        if (statusFilter === 'Failed') {
          return result.status === 'Failed' || result.status === 'Error';
        }
        return result.status === statusFilter;
      });
    }

    if (searchQuery) {
      const query = searchQuery.toLowerCase();
      filtered = filtered.filter(result =>
        result.id.toLowerCase().includes(query) ||
        result.createdAt.toLowerCase().includes(query) ||
        result.start.toLowerCase().includes(query) ||
        result.end.toLowerCase().includes(query) ||
        (result.request?.entrySettings?.filters || []).some(f => f.toLowerCase().includes(query))
      );
    }

    if (minProfit !== null) {
      filtered = filtered.filter(result => (result.holdProfit || 0) >= minProfit);
    }
    if (maxProfit !== null) {
      filtered = filtered.filter(result => (result.holdProfit || 0) <= maxProfit);
    }

    if (startDate) {
      const start = new Date(startDate);
      filtered = filtered.filter(result => new Date(result.createdAt) >= start);
    }
    if (endDate) {
      const end = new Date(endDate);
      end.setHours(23, 59, 59, 999);
      filtered = filtered.filter(result => new Date(result.createdAt) <= end);
    }

    if (!sortConfig.key) return filtered;

    return filtered.sort((a, b) => {
      if (a[sortConfig.key!] === null) return 1;
      if (b[sortConfig.key!] === null) return -1;

      let aValue = a[sortConfig.key!];
      let bValue = b[sortConfig.key!];

      if (sortConfig.key === 'createdAt' || sortConfig.key === 'start' || sortConfig.key === 'end') {
        aValue = new Date(aValue as string).getTime();
        bValue = new Date(bValue as string).getTime();
      }

      if (aValue < bValue) return sortConfig.direction === 'asc' ? -1 : 1;
      if (aValue > bValue) return sortConfig.direction === 'asc' ? 1 : -1;
      return 0;
    });
  }, [backtestResults, statusFilter, searchQuery, minProfit, maxProfit, startDate, endDate, sortConfig]);

  const hasActiveFilters = statusFilter !== 'All' || !!searchQuery || minProfit !== null || maxProfit !== null || !!startDate || !!endDate;

  return (
    <div className="min-h-screen bg-background p-4 md:p-8 pt-20 md:pt-8">
      <div className="max-w-7xl mx-auto space-y-6">
        <div className="flex justify-between items-center border-b border-border pb-4">
          <div className="flex items-center gap-3">
            <div className="flex items-center gap-3">
              <Clock />
              <MarketStatus />
              <ApiStatus />
            </div>
            <div>
              <h1 className="text-xl font-semibold tracking-tight text-foreground">Backtest Results</h1>
              <p className="text-xs text-muted-foreground mt-1">Historical strategy performance analysis</p>
            </div>
          </div>
          <div className="flex items-center gap-3">
            <div className="hidden sm:flex items-center gap-2 text-[11px] text-muted-foreground tabular-nums">
              {hasInProgress && (
                <span className="rounded-full bg-yellow-500/10 px-2.5 py-0.5 text-[11px] font-semibold text-yellow-600 dark:text-yellow-400">
                  Live · {POLL_ACTIVE_MS / 1000}s
                </span>
              )}
              <span>Updated {formatLastRefreshed(lastRefreshed)}</span>
              <button
                type="button"
                onClick={() => fetchBacktestList()}
                disabled={isRefreshing}
                className="p-1 rounded-md hover:bg-accent hover:text-foreground transition-colors disabled:opacity-50"
                title="Refresh now"
              >
                <RefreshCw className={`h-3.5 w-3.5 ${isRefreshing ? 'animate-spin' : ''}`} />
              </button>
            </div>
            <Button
              onClick={() => navigate('/backtest/create')}
              className="text-xs px-4 py-2"
            >
              New Backtest
            </Button>
          </div>
        </div>

        {error && (
          <div className="rounded-xl bg-destructive/10 border border-destructive/40 text-destructive dark:text-red-400 px-4 py-3 relative text-sm">
            <span className="font-medium">Error:</span> {error}
            <button
              onClick={() => setError('')}
              className="absolute top-2 right-2 text-destructive dark:text-red-400 hover:text-destructive/80 dark:hover:text-red-300 font-bold text-lg"
            >
              ×
            </button>
          </div>
        )}

        <div className="space-y-4">
          {isLoading ? (
            <BacktestPageSkeleton />
          ) : (
            <>
              <BacktestStatistics results={backtestResults} />

              {/* Collapsible insights — collapsed by default so the table stays primary */}
              <div className="rounded-xl border border-border/80 bg-card">
                <button
                  type="button"
                  onClick={() => setShowInsights(prev => !prev)}
                  className="w-full flex items-center justify-between rounded-xl px-4 py-3 hover:bg-accent hover:text-foreground transition-colors"
                >
                  <div className="flex items-center gap-2">
                    <BarChart3 className="h-4 w-4 text-muted-foreground" />
                    <span className="text-sm font-semibold text-foreground">
                      Insights
                    </span>
                    <span className="text-[11px] text-muted-foreground">
                      charts &amp; performers
                    </span>
                  </div>
                  {showInsights ? (
                    <ChevronDown className="h-4 w-4 text-muted-foreground" />
                  ) : (
                    <ChevronRight className="h-4 w-4 text-muted-foreground" />
                  )}
                </button>
                {showInsights && (
                  <div className="border-t border-border p-4">
                    <BacktestInsights results={backtestResults} />
                  </div>
                )}
              </div>

              {/* Filters */}
              <div className="rounded-xl border border-border/80 bg-card p-4 space-y-4">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Filter className="h-4 w-4 text-muted-foreground" />
                    <h2 className="text-sm font-semibold text-foreground">Filters</h2>
                    {!isLoading && (
                      <span className="text-[11px] text-muted-foreground tabular-nums">
                        {filteredAndSortedData.length} of {backtestResults.length}
                      </span>
                    )}
                  </div>
                  {hasActiveFilters && (
                    <Button
                      onClick={clearFilters}
                      variant="ghost"
                      size="sm"
                      className="h-7 text-xs text-muted-foreground hover:text-foreground"
                    >
                      <X className="h-3 w-3 mr-1" />
                      Clear All
                    </Button>
                  )}
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
                  <div className="space-y-1">
                    <label className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">
                      Search
                    </label>
                    <div className="relative">
                      <Search className="absolute left-2 top-1/2 transform -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                      <Input
                        type="text"
                        placeholder="ID, date, filter..."
                        value={searchQuery}
                        onChange={(e) => updateFilter('search', e.target.value)}
                        className="pl-8 text-xs h-9"
                      />
                    </div>
                  </div>

                  <div className="space-y-1">
                    <label className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">
                      Status
                    </label>
                    <select
                      value={statusFilter}
                      onChange={(e) => updateFilter('status', e.target.value)}
                      className="flex h-9 w-full rounded-lg border border-input bg-card px-3 py-2 text-xs ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
                    >
                      <option value="All">All</option>
                      <option value="Completed">Completed</option>
                      <option value="InProgress">In Progress</option>
                      <option value="Failed">Failed</option>
                    </select>
                  </div>

                  <div className="space-y-1">
                    <label className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">
                      Min Hold P/L ($)
                    </label>
                    <Input
                      type="number"
                      placeholder="Min"
                      value={minProfit !== null ? minProfit : ''}
                      onChange={(e) => updateFilter('minProfit', e.target.value)}
                      className="font-mono text-xs h-9"
                    />
                  </div>

                  <div className="space-y-1">
                    <label className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">
                      Max Hold P/L ($)
                    </label>
                    <Input
                      type="number"
                      placeholder="Max"
                      value={maxProfit !== null ? maxProfit : ''}
                      onChange={(e) => updateFilter('maxProfit', e.target.value)}
                      className="font-mono text-xs h-9"
                    />
                  </div>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="space-y-1">
                    <label className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">
                      Start Date
                    </label>
                    <Input
                      type="date"
                      value={startDate}
                      onChange={(e) => updateFilter('startDate', e.target.value)}
                      className="font-mono text-xs h-9"
                    />
                  </div>

                  <div className="space-y-1">
                    <label className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">
                      End Date
                    </label>
                    <Input
                      type="date"
                      value={endDate}
                      onChange={(e) => updateFilter('endDate', e.target.value)}
                      className="font-mono text-xs h-9"
                    />
                  </div>
                </div>
              </div>

              <BacktestResultsTable
                results={filteredAndSortedData}
                sortConfig={sortConfig}
                onSort={sortData}
                hasActiveFilters={hasActiveFilters}
                onClearFilters={clearFilters}
                totalCount={backtestResults.length}
              />
            </>
          )}
        </div>
      </div>
    </div>
  );
}

function BacktestPageSkeleton() {
  return (
    <div className="space-y-4 animate-pulse">
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-16 rounded-xl border border-border/80 bg-muted/40" />
        ))}
      </div>
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-16 rounded-xl border border-border/80 bg-muted/40" />
        ))}
      </div>
      <div className="h-12 rounded-xl border border-border/80 bg-muted/40" />
      <div className="h-40 rounded-xl border border-border/80 bg-muted/40" />
      <div className="rounded-xl border border-border/80 overflow-hidden">
        <div className="h-10 border-b border-border bg-muted/30" />
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="h-12 border-b border-border last:border-b-0 bg-muted/20" />
        ))}
      </div>
    </div>
  );
}
