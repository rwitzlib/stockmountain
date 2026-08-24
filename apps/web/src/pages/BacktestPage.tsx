import { useEffect, useState, useMemo, useCallback, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { useUser } from '@clerk/react';
import { BacktestResultsTable } from '../components/tables/BacktestResultsTable';
import { BacktestSummary } from '../components/backtest/BacktestSummary';
import { BacktestEntry, BacktestSortKey } from '../types/backtest';
import { backtestApi } from '../api/backtestApi';
import { getPercentGain } from '../utils/backtestRequest';
import { userApi, UserDetails } from '../api/userApi';
import { Clock } from '../components/clock/Clock';
import { MarketStatus } from '../components/market';
import { ApiStatus } from '../components/status';
import { Button } from '../components/ui/button';
import { RefreshCw } from 'lucide-react';

const POLL_IDLE_MS = 30000;
const POLL_ACTIVE_MS = 5000;

function formatLastRefreshed(date: Date | null): string {
  if (!date) return 'Never';
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
}

export function BacktestPage() {
  const navigate = useNavigate();
  const [error, setError] = useState('');
  const [backtestResults, setBacktestResults] = useState<BacktestEntry[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [lastRefreshed, setLastRefreshed] = useState<Date | null>(null);
  const [sortConfig, setSortConfig] = useState<{
    key: BacktestSortKey | null;
    direction: 'asc' | 'desc';
  }>({
    key: 'createdAt',
    direction: 'desc'
  });
  const hasLoadedOnce = useRef(false);
  const { user } = useUser();
  const userId = user?.id;
  const [userDetails, setUserDetails] = useState<UserDetails | null>(null);

  const inProgressCount = useMemo(
    () => backtestResults.filter(r => r.status === 'InProgress').length,
    [backtestResults]
  );
  const hasInProgress = inProgressCount > 0;

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

  // Credits change when a backtest starts or finishes, so refetch the user
  // when the in-progress count moves instead of on every poll tick.
  useEffect(() => {
    if (!userId) return;
    userApi.getUser(userId)
      .then(setUserDetails)
      .catch(e => console.error('Failed to fetch user credits:', e));
  }, [userId, inProgressCount]);

  const sortData = (key: BacktestSortKey) => {
    const direction = sortConfig.key === key && sortConfig.direction === 'asc' ? 'desc' : 'asc';
    setSortConfig({ key, direction });
  };

  const sortedData = useMemo((): BacktestEntry[] => {
    const sorted = [...backtestResults];

    if (!sortConfig.key) return sorted;

    if (sortConfig.key === 'percentGain') {
      return sorted.sort((a, b) => {
        const aGain = getPercentGain(a);
        const bGain = getPercentGain(b);
        if (aGain === null && bGain === null) return 0;
        if (aGain === null) return 1;
        if (bGain === null) return -1;
        return sortConfig.direction === 'asc' ? aGain - bGain : bGain - aGain;
      });
    }

    const sortKey = sortConfig.key as keyof BacktestEntry;

    return sorted.sort((a, b) => {
      if (a[sortKey] === null) return 1;
      if (b[sortKey] === null) return -1;

      let aValue = a[sortKey];
      let bValue = b[sortKey];

      if (sortKey === 'createdAt' || sortKey === 'start' || sortKey === 'end') {
        aValue = new Date(aValue as string).getTime();
        bValue = new Date(bValue as string).getTime();
      }

      if (aValue < bValue) return sortConfig.direction === 'asc' ? -1 : 1;
      if (aValue > bValue) return sortConfig.direction === 'asc' ? 1 : -1;
      return 0;
    });
  }, [backtestResults, sortConfig]);

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
              <BacktestSummary
                results={backtestResults}
                credits={userDetails?.credits ?? null}
                maxCredits={userDetails?.maxCredits ?? null}
              />

              <BacktestResultsTable
                results={sortedData}
                sortConfig={sortConfig}
                onSort={sortData}
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
      <div className="flex justify-end">
        <div className="h-16 w-full sm:w-96 rounded-xl border border-border/80 bg-muted/40" />
      </div>
      <div className="rounded-xl border border-border/80 overflow-hidden">
        <div className="h-10 border-b border-border bg-muted/30" />
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="h-12 border-b border-border last:border-b-0 bg-muted/20" />
        ))}
      </div>
    </div>
  );
}
