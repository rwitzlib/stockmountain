import { useEffect, useState, useMemo, useCallback } from 'react';
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
import { Search, Filter, X } from 'lucide-react';

type StatusFilter = 'All' | 'Completed' | 'InProgress' | 'Failed';

export function BacktestPage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const [error, setError] = useState('');
  const [backtestResults, setBacktestResults] = useState<BacktestEntry[]>([]);
  const [sortConfig, setSortConfig] = useState<{
    key: keyof BacktestEntry | null;
    direction: 'asc' | 'desc';
  }>({
    key: 'createdAt',
    direction: 'desc'
  });

  // Filter state from URL params
  const statusFilter = (searchParams.get('status') as StatusFilter) || 'All';
  const searchQuery = searchParams.get('search') || '';
  const minProfit = searchParams.get('minProfit') ? parseFloat(searchParams.get('minProfit')!) : null;
  const maxProfit = searchParams.get('maxProfit') ? parseFloat(searchParams.get('maxProfit')!) : null;
  const startDate = searchParams.get('startDate') || '';
  const endDate = searchParams.get('endDate') || '';

  const fetchBacktestList = useCallback(async () => {
    try {
      const apiResults = await backtestApi.getBacktests();
      setBacktestResults(apiResults);
      setError('');
    } catch (e) {
      console.error('Failed to fetch backtest list:', e);
      setError('Failed to fetch backtest data');
    }
  }, []);

  useEffect(() => {
    fetchBacktestList();
    
    // Set up periodic fetching every 30 seconds
    const interval = setInterval(fetchBacktestList, 30000);
    
    // Cleanup interval on component unmount
    return () => clearInterval(interval);
  }, [fetchBacktestList]);

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

    // Status filter
    if (statusFilter !== 'All') {
      filtered = filtered.filter(result => {
        if (statusFilter === 'Failed') {
          return result.status === 'Failed' || result.status === 'Error';
        }
        return result.status === statusFilter;
      });
    }

    // Search filter (by ID or date)
    if (searchQuery) {
      const query = searchQuery.toLowerCase();
      filtered = filtered.filter(result => 
        result.id.toLowerCase().includes(query) ||
        result.createdAt.toLowerCase().includes(query) ||
        result.start.toLowerCase().includes(query) ||
        result.end.toLowerCase().includes(query)
      );
    }

    // Profit range filter (hold = realistic P/L)
    if (minProfit !== null) {
      filtered = filtered.filter(result => 
        (result.holdProfit || 0) >= minProfit
      );
    }
    if (maxProfit !== null) {
      filtered = filtered.filter(result => 
        (result.holdProfit || 0) <= maxProfit
      );
    }

    // Date range filter
    if (startDate) {
      const start = new Date(startDate);
      filtered = filtered.filter(result => 
        new Date(result.createdAt) >= start
      );
    }
    if (endDate) {
      const end = new Date(endDate);
      end.setHours(23, 59, 59, 999); // Include entire end date
      filtered = filtered.filter(result => 
        new Date(result.createdAt) <= end
      );
    }

    // Sort
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

  const hasActiveFilters = statusFilter !== 'All' || searchQuery || minProfit !== null || maxProfit !== null || startDate || endDate;

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
              <h1 className="text-xl font-mono font-bold uppercase tracking-wider text-foreground"># Backtest Results</h1>
              <p className="text-xs font-mono text-muted-foreground mt-1">{'>> '}Historical strategy performance analysis</p>
            </div>
          </div>
          <Button
            onClick={() => navigate('/backtest/create')}
            className="bg-green-100 dark:bg-green-950 border border-green-300 dark:border-green-700 text-green-700 dark:text-green-400 hover:bg-green-200 dark:hover:bg-green-900 hover:border-green-400 dark:hover:border-green-500 font-mono text-xs uppercase px-4 py-2 transition-all"
          >
            + New Backtest
          </Button>
        </div>

        {error && (
          <div className="bg-destructive/10 dark:bg-red-950/50 border border-destructive dark:border-red-700 text-destructive dark:text-red-400 px-4 py-3 relative font-mono text-sm">
            <span className="text-destructive dark:text-red-500">ERROR:</span> {error}
            <button
              onClick={() => setError('')}
              className="absolute top-2 right-2 text-destructive dark:text-red-400 hover:text-destructive/80 dark:hover:text-red-300 font-bold text-lg"
            >
              ×
            </button>
          </div>
        )}

        <div className="space-y-4">
          <BacktestStatistics results={backtestResults} />
          
          <BacktestInsights results={backtestResults} />
          
          {/* Filters Section */}
          <div className="bg-card border border-border p-4 space-y-4">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <Filter className="h-4 w-4 text-muted-foreground" />
                <h2 className="text-sm font-mono uppercase tracking-wider text-foreground">Filters</h2>
              </div>
              {hasActiveFilters && (
                <Button
                  onClick={clearFilters}
                  variant="ghost"
                  size="sm"
                  className="h-7 text-xs font-mono text-muted-foreground hover:text-foreground"
                >
                  <X className="h-3 w-3 mr-1" />
                  Clear All
                </Button>
              )}
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
              {/* Search */}
              <div className="space-y-1">
                <label className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground">
                  Search
                </label>
                <div className="relative">
                  <Search className="absolute left-2 top-1/2 transform -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                  <Input
                    type="text"
                    placeholder="ID, date..."
                    value={searchQuery}
                    onChange={(e) => updateFilter('search', e.target.value)}
                    className="pl-8 font-mono text-xs h-9"
                  />
                </div>
              </div>

              {/* Status Filter */}
              <div className="space-y-1">
                <label className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground">
                  Status
                </label>
                <select
                  value={statusFilter}
                  onChange={(e) => updateFilter('status', e.target.value)}
                  className="flex h-9 w-full rounded-md border border-input bg-background px-3 py-2 text-xs font-mono ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
                >
                  <option value="All">All</option>
                  <option value="Completed">Completed</option>
                  <option value="InProgress">In Progress</option>
                  <option value="Failed">Failed</option>
                </select>
              </div>

              {/* Min Profit */}
              <div className="space-y-1">
                <label className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground">
                  Min Profit ($)
                </label>
                <Input
                  type="number"
                  placeholder="Min"
                  value={minProfit !== null ? minProfit : ''}
                  onChange={(e) => updateFilter('minProfit', e.target.value)}
                  className="font-mono text-xs h-9"
                />
              </div>

              {/* Max Profit */}
              <div className="space-y-1">
                <label className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground">
                  Max Profit ($)
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
              {/* Start Date */}
              <div className="space-y-1">
                <label className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground">
                  Start Date
                </label>
                <Input
                  type="date"
                  value={startDate}
                  onChange={(e) => updateFilter('startDate', e.target.value)}
                  className="font-mono text-xs h-9"
                />
              </div>

              {/* End Date */}
              <div className="space-y-1">
                <label className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground">
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
          />
        </div>
      </div>
    </div>
  );
}
