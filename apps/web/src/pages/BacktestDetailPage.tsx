import { useState, useEffect, useMemo, useCallback, useRef } from 'react';
import { useParams, useNavigate, Link, useSearchParams } from 'react-router-dom';
import { BacktestStrategyCard } from '../components/BacktestStrategyCard';
import { FilterDisplay } from '../components/backtest/FilterDisplay';
import { backtestApi } from '../api/backtestApi';
import { TradingData } from '../types/types';
import { BacktestEntry, BacktestRequest } from '../types/backtest';
import { Strategy } from '../types/strategy';
import { Button } from '../components/ui/button';
import { Card } from '../components/ui/card';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '../components/ui/dropdown-menu';
import { ArrowLeft, ChevronDown, Clock, RefreshCw, TrendingUp, MoreVertical, Copy, Bot, X } from 'lucide-react';
import { formatDateNoTimezone } from '../utils/dateFormatter';
import { formatPrice } from '../utils/chartUtils';
import { toast } from '../hooks/use-toast';
import { createChart, Time, LineSeries, ColorType, LineStyle, type IChartApi, type ISeriesApi } from 'lightweight-charts';
import { fetchMarketData } from '../services/polygon';
import { StockMarketData } from '../types/tools';
import { useQuery } from '@tanstack/react-query';

type DayOfWeek = 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday' | 'Sunday';

interface GlobalFilters {
  searchTerm: string;
  strategies: ('hold' | 'high' | 'other')[];
  profitFilter: 'all' | 'profit' | 'loss';
  dayOfWeek: DayOfWeek[];
  timeRange: { start: string; end: string } | null; // HH:MM format
  excludeTickers: string[];
}

interface BacktestDetailData {
  tradingData: TradingData | null;
  backtestEntry: BacktestEntry;
  isProcessing: boolean;
}

export function BacktestDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const [data, setData] = useState<BacktestDetailData | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [isPolling, setIsPolling] = useState(false);
  
  // Helper to map day abbreviations to full names
  const dayAbbrevToFull: Record<string, DayOfWeek> = {
    'Mon': 'Monday',
    'Tue': 'Tuesday',
    'Wed': 'Wednesday',
    'Thu': 'Thursday',
    'Fri': 'Friday',
    'Sat': 'Saturday',
    'Sun': 'Sunday'
  };

  const dayFullToAbbrev: Record<DayOfWeek, string> = {
    'Monday': 'Mon',
    'Tuesday': 'Tue',
    'Wednesday': 'Wed',
    'Thursday': 'Thu',
    'Friday': 'Fri',
    'Saturday': 'Sat',
    'Sunday': 'Sun'
  };
  

  // Chart refs and state
  const chartContainerRef = useRef<HTMLDivElement>(null);
  const chartRef = useRef<IChartApi | null>(null);
  const holdSeriesRef = useRef<ISeriesApi<'Line'> | null>(null);
  const highSeriesRef = useRef<ISeriesApi<'Line'> | null>(null);
  const spySeriesRef = useRef<ISeriesApi<'Line'> | null>(null);
  const [isDarkMode, setIsDarkMode] = useState(() => 
    document.documentElement.classList.contains('dark')
  );

  // Parse URL params to initialize filters
  const parseUrlFilters = useCallback((): GlobalFilters => {
    const daysParam = searchParams.get('days');
    const excludeParam = searchParams.get('Exclude');
    const searchParam = searchParams.get('search');
    const strategiesParam = searchParams.get('strategies');
    const profitParam = searchParams.get('profit');
    const timeStartParam = searchParams.get('timeStart');
    const timeEndParam = searchParams.get('timeEnd');

    const dayOfWeek: DayOfWeek[] = daysParam
      ? daysParam.split(',').map(day => dayAbbrevToFull[day.trim()]).filter(Boolean) as DayOfWeek[]
      : [];

    const excludeTickers: string[] = excludeParam
      ? excludeParam.split(',').map(t => t.trim().toUpperCase()).filter(t => t.length > 0)
      : [];

    const strategies: ('hold' | 'high' | 'other')[] = strategiesParam
      ? strategiesParam.split(',').filter(s => ['hold', 'high', 'other'].includes(s)) as ('hold' | 'high' | 'other')[]
      : ['hold', 'high', 'other'];

    const profitFilter: 'all' | 'profit' | 'loss' = 
      profitParam === 'profit' || profitParam === 'loss' ? profitParam : 'all';

    const timeRange = timeStartParam && timeEndParam
      ? { start: timeStartParam, end: timeEndParam }
      : null;

    return {
      searchTerm: searchParam || '',
      strategies,
      profitFilter,
      dayOfWeek,
      timeRange,
      excludeTickers
    };
  }, [searchParams]);

  // Global filters state - initialize from URL params
  const [globalFilters, setGlobalFilters] = useState<GlobalFilters>(() => parseUrlFilters());
  
  // Ref to track if we're updating filters from URL (to prevent update loop)
  const isUpdatingFromUrl = useRef(false);
  const lastSearchParamsRef = useRef<string>(searchParams.toString());

  // Sync filters when URL params change externally (e.g., browser back/forward)
  useEffect(() => {
    const currentParamsStr = searchParams.toString();
    // Only sync if URL params actually changed (not from our own updates)
    if (currentParamsStr === lastSearchParamsRef.current) {
      return;
    }
    
    lastSearchParamsRef.current = currentParamsStr;
    const urlFilters = parseUrlFilters();
    
    // Compare with current filters
    const filtersChanged = 
      urlFilters.searchTerm !== globalFilters.searchTerm ||
      urlFilters.strategies.length !== globalFilters.strategies.length ||
      !urlFilters.strategies.every(s => globalFilters.strategies.includes(s)) ||
      urlFilters.profitFilter !== globalFilters.profitFilter ||
      urlFilters.dayOfWeek.length !== globalFilters.dayOfWeek.length ||
      !urlFilters.dayOfWeek.every(d => globalFilters.dayOfWeek.includes(d)) ||
      urlFilters.excludeTickers.length !== globalFilters.excludeTickers.length ||
      !urlFilters.excludeTickers.every(t => globalFilters.excludeTickers.includes(t)) ||
      (urlFilters.timeRange?.start !== globalFilters.timeRange?.start) ||
      (urlFilters.timeRange?.end !== globalFilters.timeRange?.end);
    
    if (filtersChanged) {
      isUpdatingFromUrl.current = true;
      setGlobalFilters(urlFilters);
      // Reset flag after state update
      setTimeout(() => {
        isUpdatingFromUrl.current = false;
      }, 0);
    }
  }, [searchParams, parseUrlFilters, globalFilters]);

  // Debounced search term
  const [debouncedSearchTerm, setDebouncedSearchTerm] = useState('');

  useEffect(() => {
    if (id) {
      fetchBacktestDetails();
    }
  }, [id]);

  useEffect(() => {
    let interval: NodeJS.Timeout | null = null;
    
    // Check if backtest is in a state that requires polling
    const status = data?.backtestEntry?.status;
    const shouldPoll = status === 'Pending' || status === 'InProgress';
    
    // Set up polling if backtest is pending or in progress
    if (shouldPoll) {
      setIsPolling(true);
      interval = setInterval(async () => {
        // Fetch both entry status and results
        if (id) {
          try {
            const entry = await backtestApi.getBacktestEntry(id);
            await fetchBacktestResults(entry);
          } catch (e) {
            console.error('Failed to poll backtest status:', e);
          }
        }
      }, 15000); // Poll every 15 seconds
    } else {
      // Stop polling if status changed to completed or failed
      setIsPolling(false);
    }

    return () => {
      if (interval) {
        clearInterval(interval);
      }
    };
  }, [data?.backtestEntry?.status, id]);

  // Debounce search term
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearchTerm(globalFilters.searchTerm);
    }, 300);

    return () => clearTimeout(timer);
  }, [globalFilters.searchTerm]);

  // Update URL params when filters change
  useEffect(() => {
    // Skip if we're updating filters from URL to prevent loop
    if (isUpdatingFromUrl.current) {
      return;
    }
    
    const newParams = new URLSearchParams();
    
    if (globalFilters.dayOfWeek.length > 0) {
      newParams.set('days', globalFilters.dayOfWeek.map(d => dayFullToAbbrev[d]).join(','));
    }
    
    if (globalFilters.excludeTickers.length > 0) {
      newParams.set('Exclude', globalFilters.excludeTickers.join(','));
    }
    
    if (globalFilters.searchTerm) {
      newParams.set('search', globalFilters.searchTerm);
    }
    
    if (globalFilters.strategies.length !== 3 || !globalFilters.strategies.includes('hold') || !globalFilters.strategies.includes('high') || !globalFilters.strategies.includes('other')) {
      newParams.set('strategies', globalFilters.strategies.join(','));
    }
    
    if (globalFilters.profitFilter !== 'all') {
      newParams.set('profit', globalFilters.profitFilter);
    }
    
    if (globalFilters.timeRange) {
      newParams.set('timeStart', globalFilters.timeRange.start);
      newParams.set('timeEnd', globalFilters.timeRange.end);
    }
    
    // Only update URL if params actually changed
    const currentParams = searchParams.toString();
    const newParamsStr = newParams.toString();
    
    if (currentParams !== newParamsStr) {
      lastSearchParamsRef.current = newParamsStr; // Update ref to prevent sync effect from triggering
      setSearchParams(newParams, { replace: true });
    }
  }, [globalFilters, dayFullToAbbrev, searchParams, setSearchParams]);

  // Filter update handlers
  const updateFilters = useCallback((updates: Partial<GlobalFilters>) => {
    setGlobalFilters(prev => ({ ...prev, ...updates }));
  }, []);

  const clearAllFilters = useCallback(() => {
    setGlobalFilters({
      searchTerm: '',
      strategies: ['hold', 'high', 'other'],
      profitFilter: 'all',
      dayOfWeek: [],
      timeRange: null,
      excludeTickers: []
    });
    // Clear URL params
    lastSearchParamsRef.current = ''; // Update ref to prevent sync effect from triggering
    setSearchParams({}, { replace: true });
  }, [setSearchParams]);

  const hasActiveFilters = useMemo(() => {
    return globalFilters.searchTerm !== '' ||
           globalFilters.strategies.length !== 3 ||
           globalFilters.profitFilter !== 'all' ||
           globalFilters.dayOfWeek.length > 0 ||
           globalFilters.timeRange !== null ||
           globalFilters.excludeTickers.length > 0;
  }, [globalFilters]);

  // Calculate strategy statistics from filtered trade data
  const calculateStrategyStats = useCallback((trades: any[], strategyType: 'hold' | 'high' | 'other') => {
    const strategyTrades = trades.filter(trade => trade.strategy === strategyType);
    
    if (strategyTrades.length === 0) {
      return {
        endBalance: 0,
        balanceChange: 0,
        sumProfit: 0,
        winRatio: 0,
        avgWin: 0,
        avgLoss: 0,
        maxConcurrentPositions: 0,
        totalTradesTaken: 0
      };
    }

    const profits = strategyTrades.map(trade => trade.profit);
    const winningTrades = profits.filter(p => p > 0);
    const losingTrades = profits.filter(p => p < 0);
    
    const sumProfit = profits.reduce((sum, profit) => sum + profit, 0);
    const avgWin = winningTrades.length > 0 ? winningTrades.reduce((sum, profit) => sum + profit, 0) / winningTrades.length : 0;
    const avgLoss = losingTrades.length > 0 ? Math.abs(losingTrades.reduce((sum, profit) => sum + profit, 0) / losingTrades.length) : 0;
    const winRatio = winningTrades.length / strategyTrades.length;

    // For filtered data, we'll use the sum of position values as a proxy for balance
    const totalPosition = strategyTrades.reduce((sum, trade) => sum + (trade.shares * trade.startPrice), 0);
    const endBalance = totalPosition + sumProfit;

    // For filtered data, calculate max concurrent positions from the filtered trades
    // This is a simplified calculation - for accurate results, we'd need to track position opens/closes
    const maxConcurrentPositions = 0; // Will be calculated from tradingData if available
    const totalTradesTaken = strategyTrades.length;

    return {
      endBalance,
      balanceChange: sumProfit,
      sumProfit,
      winRatio,
      avgWin,
      avgLoss,
      maxConcurrentPositions,
      totalTradesTaken
    };
  }, []);

  const fetchBacktestDetails = async () => {
    try {
      setIsLoading(true);
      setError('');
      
      const entry = await backtestApi.getBacktestEntry(id!);
      await fetchBacktestResults(entry);
      
    } catch (e) {
      setError('Failed to fetch backtest details');
      console.error(e);
    } finally {
      setIsLoading(false);
    }
  };

  const fetchBacktestResults = async (entry?: BacktestEntry) => {
    try {
      // If entry is provided, use it; otherwise fetch the latest entry status
      let backtestEntry = entry;
      if (!backtestEntry && id) {
        backtestEntry = await backtestApi.getBacktestEntry(id);
      }
      if (!backtestEntry) return;

      const status = backtestEntry.status;
      const isPending = status === 'Pending';
      const isInProgress = status === 'InProgress';
      const isCompleted = status === 'Completed';
      const isFailed = status === 'Failed';

      // Only fetch results if not pending/in-progress (to avoid unnecessary API calls)
      if (isPending || isInProgress) {
        // Backtest is still processing
        setData({
          tradingData: null,
          backtestEntry: backtestEntry,
          isProcessing: true
        });
        return;
      }

      // Fetch results for completed backtests
      if (isCompleted || isFailed) {
        try {
          const result = await backtestApi.getBacktestResult(backtestEntry.id);
          
          // Check if result indicates processing is still ongoing (edge case)
          const isProcessingResponse = Array.isArray(result) && 
            result.length === 1 && 
            typeof result[0] === 'string' && 
            result[0].toLowerCase().includes('not completed');

          if (isProcessingResponse && !isFailed) {
            // Status says completed but result says not completed - treat as processing
            setData({
              tradingData: null,
              backtestEntry: backtestEntry,
              isProcessing: true
            });
          } else {
            // Backtest is completed or failed
            setData({
              tradingData: isFailed ? null : result,
              backtestEntry: backtestEntry,
              isProcessing: false
            });
          }
        } catch (resultError) {
          // If result fetch fails but we have entry, still update entry status
          setData({
            tradingData: null,
            backtestEntry: backtestEntry,
            isProcessing: false
          });
        }
      }
    } catch (e) {
      console.error('Failed to fetch backtest results:', e);
      // If we have entry details, still show them even if results fail
      if (entry) {
        const status = entry.status;
        setData({
          tradingData: null,
          backtestEntry: entry,
          isProcessing: status === 'Pending' || status === 'InProgress'
        });
      }
    }
  };

  const handleRefreshResults = () => {
    if (data?.backtestEntry) {
      fetchBacktestResults();
    }
  };

  // Fetch SPY data for comparison
  const { data: spyDataResponse } = useQuery({
    queryKey: ['spyData', id, data?.backtestEntry?.start, data?.backtestEntry?.end],
    queryFn: async () => {
      if (!data?.backtestEntry?.start || !data?.backtestEntry?.end) {
        return null;
      }

      const startDate = new Date(data.backtestEntry.start);
      startDate.setHours(0, 0, 0, 0);
      
      const endDate = new Date(data.backtestEntry.end);
      endDate.setHours(23, 59, 59, 999);

      const fromDate = startDate.toISOString().split('T')[0];
      const toDate = endDate.toISOString().split('T')[0];

      try {
        const data = await fetchMarketData({
          ticker: 'SPY',
          multiplier: 1,
          timespan: 'day',
          from: fromDate,
          to: toDate
        });
        return data;
      } catch (error) {
        console.error('Error fetching SPY data:', error);
        return null;
      }
    },
    enabled: !!data?.backtestEntry?.start && !!data?.backtestEntry?.end && !!data?.tradingData,
  });

  const handleCopyBacktest = () => {
    const copyData = getCopyData();
    if (!copyData) {
      toast({
        title: 'Copy unavailable',
        description: 'Unable to reconstruct this backtest configuration.',
        variant: 'destructive',
      });
      return;
    }

    navigate('/backtest', { state: { backtestDefaults: copyData } });
  };

  const handleCreateStrategy = () => {
    if (!data?.backtestEntry) return;
    
    const strategyData = mapBacktestToStrategy(data.backtestEntry);
    
    // Navigate to dashboard with strategy data as state
    navigate('/optimus/dashboard', { 
      state: { 
        createStrategy: true,
        initialStrategyData: strategyData 
      } 
    });
  };

  const mapBacktestToStrategy = (backtestEntry: BacktestEntry): Strategy => {
    const requestData = getRequestData(backtestEntry);
    
    return {
      id: '', // Will be generated by StrategyForm
      name: `Strategy from Backtest ${backtestEntry.id.slice(0, 8)}`,
      type: 'Paper' as const,
      integration: 'Default' as const,
      state: 'inactive' as const,
      visibility: 'private' as const,
      positionInfo: requestData.positionInfo,
      exitInfo: {
        stopLoss: requestData.exitInfo.stopLoss,
        profitTarget: requestData.exitInfo.profitTarget,
        timeframe: requestData.exitInfo.timeframe,
      },
      argument: requestData.argument || {
        operator: 'AND',
        filters: []
      }
    };
  };

  const getCopyData = (): BacktestRequest | undefined => {
    if (!data?.backtestEntry) return undefined;

    const { backtestEntry } = data;
    const requestData = getRequestData(backtestEntry);
    const positionInfo = requestData.positionInfo;
    const exitInfo = requestData.exitInfo;

    const request: BacktestRequest = {
      start: backtestEntry.start ? backtestEntry.start.slice(0, 10) : '',
      end: backtestEntry.end ? backtestEntry.end.slice(0, 10) : '',
      PositionSettings: {
        StartingBalance: positionInfo.startingBalance ?? 10000,
        AllowSimultaneous: (positionInfo as any).allowSimultaneous ?? ((positionInfo.maxConcurrentPositions ?? 1) > 1),
        MaxConcurrentPositions: positionInfo.maxConcurrentPositions ?? 1,
        Model: {
          Type: ((positionInfo as any).modelType) ?? 'Fixed',
          Size: positionInfo.positionSize ?? 1000,
        },
      },
      EntrySettings: {
        Filters: requestData.filters || [],
      },
      ExitSettings: {},
    };

    if (exitInfo.profitTarget) {
      request.ExitSettings.TakeProfit = {
        CandleType: exitInfo.profitTarget.candleType ?? 'PreviousCandle',
        Type: exitInfo.profitTarget.type ?? 'percent',
        Value: exitInfo.profitTarget.value ?? 0,
        PriceActionType: exitInfo.profitTarget.priceActionType ?? 'close',
      };
    }

    if (exitInfo.stopLoss) {
      request.ExitSettings.StopLoss = {
        CandleType: exitInfo.stopLoss.candleType ?? 'PreviousCandle',
        Type: exitInfo.stopLoss.type ?? 'percent',
        Value: exitInfo.stopLoss.value ?? 0,
        PriceActionType: exitInfo.stopLoss.priceActionType ?? 'close',
      };
    }

    if (exitInfo.timeframe) {
      request.ExitSettings.TimedExit = {
        Timeframe: {
          Multiplier: exitInfo.timeframe.multiplier ?? 1,
          Timespan: exitInfo.timeframe.timespan ?? 'minute',
        },
      };
    }

    return request;
  };

  const formatStopConfig = (config: any) => {
    if (!config) return 'Not set';
    
    const priceActionDisplay = config.priceActionType ? `${config.priceActionType} ` : '';
    
    if (config.type === 'percent') {
      return `${priceActionDisplay}${config.value}%`;
    }
    if (config.type === 'flat' || config.type === 'value') {
      return `${priceActionDisplay}$${config.value}`;
    }
    // Fallback for other types or legacy data
    return `${priceActionDisplay}${config.value} (${config.type || 'unknown type'})`;
  };

  const formatTimeframe = (timeframe: any) => {
    if (!timeframe) return 'Not set';
    return `${timeframe.multiplier} ${timeframe.timespan}${timeframe.multiplier > 1 ? 's' : ''}`;
  };

  // Helper function to normalize request data (handles both 'request' and 'requestDetails' formats)
  const getRequestData = useCallback((backtestEntry: BacktestEntry) => {
    // Try new format first (request)
    if ((backtestEntry as any).request) {
      const req = (backtestEntry as any).request;
      return {
        positionInfo: {
          startingBalance: req.positionSettings?.startingBalance ?? 10000,
          maxConcurrentPositions: req.positionSettings?.maxConcurrentPositions ?? 1,
          positionSize: req.positionSettings?.model?.size ?? 1000,
          allowSimultaneous: req.positionSettings?.allowSimultaneous ?? false,
          modelType: req.positionSettings?.model?.type ?? 'Fixed',
        },
        exitInfo: {
          stopLoss: req.exitSettings?.stopLoss,
          profitTarget: req.exitSettings?.takeProfit,
          timeframe: req.exitSettings?.timedExit?.timeframe,
          avoidOvernight: req.exitSettings?.timedExit?.avoidOvernight,
        },
        argument: req.entrySettings?.filters ? {
          operator: 'AND' as const,
          filters: req.entrySettings.filters.map((f: string) => ({ expression: f }))
        } : undefined,
        filters: req.entrySettings?.filters || [],
      };
    }
    
    // Fall back to old format (requestDetails)
    if (backtestEntry.requestDetails) {
      return {
        positionInfo: backtestEntry.requestDetails.positionInfo || {
          startingBalance: 10000,
          maxConcurrentPositions: 1,
          positionSize: 1000,
        },
        exitInfo: backtestEntry.requestDetails.exitInfo,
        argument: backtestEntry.requestDetails.argument,
        filters: [],
      };
    }
    
    // Default fallback
    return {
      positionInfo: {
        startingBalance: 10000,
        maxConcurrentPositions: 1,
        positionSize: 1000,
      },
      exitInfo: {},
      argument: undefined,
      filters: [],
    };
  }, []);

  // Helper to format duration
  const formatDuration = (seconds: number | undefined) => {
    if (!seconds) return 'N/A';
    if (seconds < 60) return `${seconds}s`;
    if (seconds < 3600) return `${Math.floor(seconds / 60)}m ${seconds % 60}s`;
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const secs = seconds % 60;
    return `${hours}h ${minutes}m ${secs}s`;
  };

  // Helper function to check if a trade timestamp matches the time range filter
  const isTradeWithinTimeRange = useCallback((timestamp: string, timeRange: { start: string; end: string } | null) => {
    if (!timeRange) return true;
    
    const date = new Date(timestamp);
    const hours = date.getHours().toString().padStart(2, '0');
    const minutes = date.getMinutes().toString().padStart(2, '0');
    const timeString = `${hours}:${minutes}`;
    
    return timeString >= timeRange.start && timeString <= timeRange.end;
  }, []);

  // Helper function to check if an entry matches the date range filter
  const isEntryWithinDayOfWeek = useCallback((entryDate: string, dayOfWeek: DayOfWeek[]) => {
    if (dayOfWeek.length === 0) return true;
    
    // Extract date part to avoid timezone issues
    // Handle formats like "2025-01-06" or "2025-01-06T00:00:00Z"
    const dateStr = entryDate.includes('T') ? entryDate.split('T')[0] : entryDate.split(' ')[0];
    const [year, month, day] = dateStr.split('-').map(Number);
    
    // Create date using UTC to avoid timezone shifts
    // Use UTC methods to get the day of week
    const date = new Date(Date.UTC(year, month - 1, day));
    const dayNames: DayOfWeek[] = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
    const dayName = dayNames[date.getUTCDay()];
    
    return dayOfWeek.includes(dayName);
  }, []);

  // Helper function to check if an entry contains trades matching strategy and profit filters
  const entryMatchesFilters = useCallback((entry: any, strategies: string[], profitFilter: string, searchTerm: string, excludeTickers: string[]) => {
    // Check each result in the entry
    return entry.results.some((result: any) => {
      // Check if ticker is excluded
      if (excludeTickers.length > 0 && excludeTickers.includes(result.ticker.toUpperCase())) {
        return false;
      }

      // Check if any of the selected strategies have trades
      const hasMatchingStrategy = strategies.some(strategy => {
        return result[strategy] && result[strategy].profit !== undefined;
      });
      
      if (!hasMatchingStrategy) return false;

      // If there's a search term, check if ticker matches
      if (searchTerm) {
        const searchLower = searchTerm.toLowerCase();
        if (!result.ticker.toLowerCase().includes(searchLower)) {
          return false;
        }
      }

      // Check profit filter against matching strategies
      if (profitFilter !== 'all') {
        const hasMatchingProfit = strategies.some(strategy => {
          const strategyResult = result[strategy];
          if (!strategyResult) return false;
          
          if (profitFilter === 'profit') {
            return strategyResult.profit > 0;
          } else if (profitFilter === 'loss') {
            return strategyResult.profit < 0;
          }
          return true;
        });
        
        if (!hasMatchingProfit) return false;
      }

      return true;
    });
  }, []);

  // Calculate all strategy statistics from entries
  const allStrategyStats = useMemo(() => {
    if (!data?.tradingData?.entries) {
      return {
        hold: { endBalance: 0, balanceChange: 0, sumProfit: 0, winRatio: 0, avgWin: 0, avgLoss: 0, maxConcurrentPositions: 0, totalTradesTaken: 0 },
        high: { endBalance: 0, balanceChange: 0, sumProfit: 0, winRatio: 0, avgWin: 0, avgLoss: 0, maxConcurrentPositions: 0, totalTradesTaken: 0 },
        other: { endBalance: 0, balanceChange: 0, sumProfit: 0, winRatio: 0, avgWin: 0, avgLoss: 0, maxConcurrentPositions: 0, totalTradesTaken: 0 }
      };
    }

    const calculateStatsForStrategy = (strategyKey: 'hold' | 'high' | 'other') => {
      const trades: number[] = [];
      let totalPosition = 0;

      data.tradingData!.entries.forEach(entry => {
        entry.results.forEach(result => {
          const strategyResult = result[strategyKey];
          if (strategyResult && strategyResult.profit !== undefined) {
            trades.push(strategyResult.profit);
            totalPosition += result.shares * result.startPrice;
          }
        });
      });

      if (trades.length === 0) {
        return { endBalance: 0, balanceChange: 0, sumProfit: 0, winRatio: 0, avgWin: 0, avgLoss: 0, maxConcurrentPositions: 0, totalTradesTaken: 0 };
      }

             const sumProfit = trades.reduce((sum, profit) => sum + profit, 0);
       const winningTrades = trades.filter(p => p > 0);
       const losingTrades = trades.filter(p => p < 0);
       const avgWin = winningTrades.length > 0 ? winningTrades.reduce((sum, p) => sum + p, 0) / winningTrades.length : 0;
       const avgLoss = losingTrades.length > 0 ? Math.abs(losingTrades.reduce((sum, p) => sum + p, 0) / losingTrades.length) : 0;
       const winRatio = winningTrades.length / trades.length;
       const startingBalance = data?.backtestEntry ? getRequestData(data.backtestEntry).positionInfo.startingBalance || 10000 : 10000;
       const endBalance = startingBalance + sumProfit;

      // Get maxConcurrentPositions and totalTradesTaken from tradingData if available
      const strategyData = data.tradingData[strategyKey];
      const maxConcurrentPositions = strategyData?.maxConcurrentPositions ?? 0;
      const totalTradesTaken = strategyData?.totalTradesTaken ?? trades.length;

      return {
        endBalance,
        balanceChange: sumProfit,
        sumProfit,
        winRatio,
        avgWin,
        avgLoss,
        maxConcurrentPositions,
        totalTradesTaken,
        averageDailyReturn: strategyData?.averageDailyReturn,
        dailyReturnStdDev: strategyData?.dailyReturnStdDev,
        sharpeRatio: strategyData?.sharpeRatio,
        maxDrawdown: strategyData?.maxDrawdown,
        profitFactor: strategyData?.profitFactor
      };
    };

    return {
      hold: calculateStatsForStrategy('hold'),
      high: calculateStatsForStrategy('high'),
      other: calculateStatsForStrategy('other')
    };
  }, [data?.tradingData?.entries, data?.tradingData]);

  // Calculate daily results from entries for charts
  const calculatedDailyResults = useMemo(() => {
    if (!data?.tradingData?.entries) return [];

    const dailyMap = new Map<string, any>();

    data.tradingData.entries.forEach(entry => {
      const date = new Date(entry.date).toISOString().split('T')[0];
      
      if (!dailyMap.has(date)) {
        dailyMap.set(date, {
          date: date,
          hold: { totalBalance: 0, profit: 0, bought: [], sold: [] },
          high: { totalBalance: 0, profit: 0, bought: [], sold: [] },
          other: { totalBalance: 0, profit: 0, bought: [], sold: [] }
        });
      }

      const dayData = dailyMap.get(date);

      entry.results.forEach(result => {
        ['hold', 'high', 'other'].forEach(strategy => {
          const strategyResult = result[strategy];
          if (strategyResult && strategyResult.profit !== undefined) {
            dayData[strategy].profit += strategyResult.profit;
            dayData[strategy].sold.push({
              ticker: result.ticker,
              price: strategyResult.endPrice,
              shares: result.shares,
              position: result.shares * strategyResult.endPrice,
              profit: strategyResult.profit,
              timestamp: strategyResult.soldAt,
              stoppedOut: strategyResult.stoppedOut
            });
          }
        });
      });
    });

    // Calculate cumulative balances
    const sortedDays = Array.from(dailyMap.values()).sort((a, b) => 
      new Date(a.date).getTime() - new Date(b.date).getTime()
    );

         // Get starting balance from backtest configuration
     const startingBalance = data?.backtestEntry ? getRequestData(data.backtestEntry).positionInfo.startingBalance || 10000 : 10000;
     let holdBalance = startingBalance;
     let highBalance = startingBalance;
     let otherBalance = startingBalance;

    sortedDays.forEach(day => {
      holdBalance += day.hold.profit;
      highBalance += day.high.profit;
      otherBalance += day.other.profit;

      day.hold.totalBalance = holdBalance;
      day.high.totalBalance = highBalance;
      day.other.totalBalance = otherBalance;
    });

    return sortedDays;
  }, [data?.tradingData?.entries]);

  // Get raw flattened trade results from entries - memoized
  const rawTradeResults = useMemo(() => {
    if (!data?.tradingData?.entries) return [];
    
    const results: any[] = [];
    data.tradingData.entries.forEach(entry => {
      entry.results.forEach(result => {
        // Add trades for each strategy (hold, high, other)
        ['hold', 'high', 'other'].forEach(strategy => {
          const strategyResult = result[strategy];
          if (strategyResult && strategyResult.profit !== undefined) {
            results.push({
              ticker: result.ticker,
              boughtAt: result.boughtAt,
              soldAt: strategyResult.soldAt,
              startPrice: result.startPrice,
              endPrice: strategyResult.endPrice,
              shares: result.shares,
              profit: strategyResult.profit,
              strategy: strategy,
              stoppedOut: strategyResult.stoppedOut,
              entryId: entry.entryId,
              entryDate: entry.date
            });
          }
        });
      });
    });
    
    return results;
  }, [data?.tradingData?.entries]);

  // Apply global filters at the TradingEntry level - memoized with debounced search
  const filteredTradingEntries = useMemo(() => {
    if (!data?.tradingData?.entries) return [];
    
    return data.tradingData.entries.filter(entry => {
      // Date range filter (using entry date)
      if (!isEntryWithinDayOfWeek(entry.date, globalFilters.dayOfWeek)) {
        return false;
      }

      // Check if entry contains trades matching other filters (excluding time - that's done at trade level)
      if (!entryMatchesFilters(entry, globalFilters.strategies, globalFilters.profitFilter, debouncedSearchTerm, globalFilters.excludeTickers)) {
        return false;
      }

      return true;
    });
  }, [data?.tradingData?.entries, debouncedSearchTerm, globalFilters, isEntryWithinDayOfWeek, entryMatchesFilters]);

  // Flatten filtered entries into individual trade results
  const filteredTradeResults = useMemo(() => {
    const results: any[] = [];
    
    filteredTradingEntries.forEach(entry => {
      entry.results.forEach((result: any) => {
        // Add trades for each strategy (hold, high, other) that match current filters
        globalFilters.strategies.forEach(strategy => {
          if (result[strategy] && result[strategy].profit !== undefined) {
            // Apply search filter at trade level
            if (debouncedSearchTerm) {
              const searchLower = debouncedSearchTerm.toLowerCase();
              if (!result.ticker.toLowerCase().includes(searchLower)) {
                return;
              }
            }

            // Apply profit filter at trade level
            if (globalFilters.profitFilter === 'profit' && result[strategy].profit <= 0) {
              return;
            } else if (globalFilters.profitFilter === 'loss' && result[strategy].profit >= 0) {
              return;
            }

                    // Apply time range filter at trade level (using boughtAt time)
        if (!isTradeWithinTimeRange(result.boughtAt, globalFilters.timeRange)) {
              return;
            }

            // Apply exclude tickers filter
            if (globalFilters.excludeTickers.length > 0 && globalFilters.excludeTickers.includes(result.ticker.toUpperCase())) {
              return;
            }

            results.push({
              ticker: result.ticker,
              boughtAt: result.boughtAt,
              soldAt: result[strategy].soldAt,
              startPrice: result.startPrice,
              endPrice: result[strategy].endPrice,
              shares: result.shares,
              profit: result[strategy].profit,
              strategy: strategy,
              stoppedOut: result[strategy].stoppedOut,
              entryId: entry.entryId,
              entryDate: entry.date
            });
          }
        });
      });
    });
    
    return results;
  }, [filteredTradingEntries, globalFilters.strategies, globalFilters.profitFilter, globalFilters.timeRange, debouncedSearchTerm, isTradeWithinTimeRange]);

  // Calculate filtered strategy statistics
  const filteredStrategyStats = useMemo(() => {
    const holdStats = calculateStrategyStats(filteredTradeResults, 'hold');
    const highStats = calculateStrategyStats(filteredTradeResults, 'high');
    const otherStats = calculateStrategyStats(filteredTradeResults, 'other');
    
    // Include overall metrics from original tradingData (these are calculated from full dataset)
    const holdData = data?.tradingData?.hold;
    const highData = data?.tradingData?.high;
    const otherData = data?.tradingData?.other;
    
    return {
      hold: {
        ...holdStats,
        averageDailyReturn: holdData?.averageDailyReturn,
        dailyReturnStdDev: holdData?.dailyReturnStdDev,
        sharpeRatio: holdData?.sharpeRatio,
        maxDrawdown: holdData?.maxDrawdown,
        profitFactor: holdData?.profitFactor
      },
      high: {
        ...highStats,
        averageDailyReturn: highData?.averageDailyReturn,
        dailyReturnStdDev: highData?.dailyReturnStdDev,
        sharpeRatio: highData?.sharpeRatio,
        maxDrawdown: highData?.maxDrawdown,
        profitFactor: highData?.profitFactor
      },
      other: {
        ...otherStats,
        averageDailyReturn: otherData?.averageDailyReturn,
        dailyReturnStdDev: otherData?.dailyReturnStdDev,
        sharpeRatio: otherData?.sharpeRatio,
        maxDrawdown: otherData?.maxDrawdown,
        profitFactor: otherData?.profitFactor
      }
    };
  }, [filteredTradeResults, calculateStrategyStats, data?.tradingData]);

  // Create filtered daily results from filtered entries
  const filteredDailyResults = useMemo(() => {
    if (!hasActiveFilters) {
      return calculatedDailyResults;
    }

    // Recalculate daily results from filtered entries only
    const dailyMap = new Map<string, any>();

    filteredTradingEntries.forEach(entry => {
      const date = new Date(entry.date).toISOString().split('T')[0];
      
      if (!dailyMap.has(date)) {
        dailyMap.set(date, {
          date: date,
          hold: { totalBalance: 0, profit: 0, bought: [], sold: [] },
          high: { totalBalance: 0, profit: 0, bought: [], sold: [] },
          other: { totalBalance: 0, profit: 0, bought: [], sold: [] }
        });
      }

      const dayData = dailyMap.get(date);

      entry.results.forEach(result => {
        // Apply additional filters at the trade level
        globalFilters.strategies.forEach(strategy => {
          const strategyResult = result[strategy];
          if (strategyResult && strategyResult.profit !== undefined) {
            // Apply search filter at trade level
            if (debouncedSearchTerm) {
              const searchLower = debouncedSearchTerm.toLowerCase();
              if (!result.ticker.toLowerCase().includes(searchLower)) {
                return;
              }
            }

            // Apply profit filter at trade level
            if (globalFilters.profitFilter === 'profit' && strategyResult.profit <= 0) {
              return;
            } else if (globalFilters.profitFilter === 'loss' && strategyResult.profit >= 0) {
              return;
            }

            // Apply time range filter at trade level (using boughtAt time)
            if (!isTradeWithinTimeRange(result.boughtAt, globalFilters.timeRange)) {
              return;
            }

            // Apply exclude tickers filter
            if (globalFilters.excludeTickers.length > 0 && globalFilters.excludeTickers.includes(result.ticker.toUpperCase())) {
              return;
            }

            dayData[strategy].profit += strategyResult.profit;
            dayData[strategy].sold.push({
              ticker: result.ticker,
              price: strategyResult.endPrice,
              shares: result.shares,
              position: result.shares * strategyResult.endPrice,
              profit: strategyResult.profit,
              timestamp: strategyResult.soldAt,
              stoppedOut: strategyResult.stoppedOut
            });
          }
        });
      });
    });

    // Calculate cumulative balances
    const sortedDays = Array.from(dailyMap.values()).sort((a, b) => 
      new Date(a.date).getTime() - new Date(b.date).getTime()
    );

    // Get starting balance from backtest configuration
    const startingBalance = data?.backtestEntry ? getRequestData(data.backtestEntry).positionInfo.startingBalance || 10000 : 10000;
    let holdBalance = startingBalance;
    let highBalance = startingBalance;
    let otherBalance = startingBalance;

    sortedDays.forEach(day => {
      holdBalance += day.hold.profit;
      highBalance += day.high.profit;
      otherBalance += day.other.profit;

      day.hold.totalBalance = holdBalance;
      day.high.totalBalance = highBalance;
      day.other.totalBalance = otherBalance;
    });

    return sortedDays;
  }, [filteredTradingEntries, globalFilters, debouncedSearchTerm, isTradeWithinTimeRange, data?.backtestEntry, getRequestData]);

  // Detect theme changes
  useEffect(() => {
    const observer = new MutationObserver(() => {
      setIsDarkMode(document.documentElement.classList.contains('dark'));
    });
    observer.observe(document.documentElement, {
      attributes: true,
      attributeFilter: ['class'],
    });
    return () => observer.disconnect();
  }, []);

  // Transform daily results to chart data format
  const getChartData = useCallback(() => {
    const dailyResults = hasActiveFilters ? filteredDailyResults : calculatedDailyResults;
    if (!dailyResults || dailyResults.length === 0) {
      return { holdData: [], highData: [] };
    }

    const holdData: { time: number; value: number }[] = [];
    const highData: { time: number; value: number }[] = [];

    dailyResults.forEach(day => {
      const date = new Date(day.date);
      date.setHours(16, 0, 0, 0); // Use 4 PM (market close) for daily data points
      const timestamp = Math.floor(date.getTime() / 1000);

      holdData.push({
        time: timestamp,
        value: day.hold.totalBalance
      });

      highData.push({
        time: timestamp,
        value: day.high.totalBalance
      });
    });

    return { holdData, highData };
  }, [calculatedDailyResults, filteredDailyResults, hasActiveFilters]);

  // Calculate SPY performance data
  const getSpyPerformanceData = useCallback(() => {
    if (!spyDataResponse || !spyDataResponse.results || spyDataResponse.results.length === 0 || !data?.backtestEntry) {
      return [];
    }

    const startingBalance = getRequestData(data.backtestEntry).positionInfo.startingBalance || 10000;
    
    // Sort SPY data by timestamp
    const sortedSpyBars = [...spyDataResponse.results].sort((a, b) => a.t - b.t);
    
    if (sortedSpyBars.length === 0) {
      return [];
    }

    // Get the first SPY bar's close price to calculate initial shares
    const firstBar = sortedSpyBars[0];
    const initialPrice = firstBar.c;
    const initialShares = startingBalance / initialPrice;

    // Get backtest start date
    const startDate = new Date(data.backtestEntry.start);
    startDate.setHours(0, 0, 0, 0);
    const startTimestamp = Math.floor(startDate.getTime() / 1000);

    // Create SPY performance data points
    const spyDataPoints: { time: number; value: number }[] = [];
    
    // Add starting point at the beginning of the backtest period
    spyDataPoints.push({
      time: startTimestamp,
      value: startingBalance
    });

    // Add data points for each SPY bar
    sortedSpyBars.forEach(bar => {
      const portfolioValue = initialShares * bar.c;
      const barTimestamp = Math.floor(bar.t / 1000); // Convert milliseconds to seconds
      
      // Only add if the bar date is on or after the start date
      if (barTimestamp >= startTimestamp) {
        spyDataPoints.push({
          time: barTimestamp,
          value: portfolioValue
        });
      }
    });

    return spyDataPoints;
  }, [spyDataResponse, data?.backtestEntry, getRequestData]);

  // Theme helpers
  const applyTheme = () => {
    if (!chartRef.current) return;
    const colors = isDarkMode ? {
      background: '#0b1220',
      text: '#e5e7eb',
      grid: '#1f2937',
      crosshair: '#6b7280',
    } : {
      background: '#ffffff',
      text: '#1f2937',
      grid: '#e5e7eb',
      crosshair: '#9ca3af',
    };
    chartRef.current.applyOptions({
      layout: {
        background: { type: ColorType.Solid, color: colors.background },
        textColor: colors.text,
      },
      grid: {
        vertLines: { color: colors.grid },
        horzLines: { color: colors.grid },
      },
      crosshair: {
        mode: 0,
        vertLine: { color: colors.crosshair },
        horzLine: { color: colors.crosshair },
      },
    });
  };

  // Create chart when data becomes available
  useEffect(() => {
    if (!chartContainerRef.current || !data?.tradingData) return;
    if (chartRef.current) return; // already created
    
    const initialIsDark = document.documentElement.classList.contains('dark');
    const initialColors = initialIsDark ? {
      background: '#0b1220',
      text: '#e5e7eb',
      grid: '#1f2937',
      crosshair: '#6b7280',
      border: '#374151',
    } : {
      background: '#ffffff',
      text: '#1f2937',
      grid: '#e5e7eb',
      crosshair: '#9ca3af',
      border: '#d1d5db',
    };
    
    const chart = createChart(chartContainerRef.current, {
      width: chartContainerRef.current.clientWidth,
      height: chartContainerRef.current.clientHeight || 600,
      layout: {
        background: { type: ColorType.Solid, color: initialColors.background },
        textColor: initialColors.text,
      },
      crosshair: { mode: 0, vertLine: { color: initialColors.crosshair }, horzLine: { color: initialColors.crosshair } },
      grid: { vertLines: { color: initialColors.grid }, horzLines: { color: initialColors.grid } },
      timeScale: { timeVisible: true, secondsVisible: false, lockVisibleTimeRangeOnResize: true },
      rightPriceScale: { borderColor: initialColors.border },
      autoSize: true,
    });
    chartRef.current = chart;

    const holdLine = chart.addSeries(LineSeries, {
      color: '#8b5cf6',
      lineWidth: 3,
      crosshairMarkerVisible: true,
      crosshairMarkerRadius: 6,
      crosshairMarkerBorderColor: initialIsDark ? '#ffffff' : '#1f2937',
      crosshairMarkerBackgroundColor: '#8b5cf6',
      title: 'Hold Strategy',
    });
    holdSeriesRef.current = holdLine;

    const highLine = chart.addSeries(LineSeries, {
      color: '#16a34a',
      lineWidth: 3,
      crosshairMarkerVisible: true,
      crosshairMarkerRadius: 6,
      crosshairMarkerBorderColor: initialIsDark ? '#ffffff' : '#1f2937',
      crosshairMarkerBackgroundColor: '#16a34a',
      title: 'High Strategy',
    });
    highSeriesRef.current = highLine;

    // Add SPY comparison series
    const spyLine = chart.addSeries(LineSeries, {
      color: '#f59e0b',
      lineWidth: 2,
      crosshairMarkerVisible: true,
      crosshairMarkerRadius: 5,
      crosshairMarkerBorderColor: initialIsDark ? '#ffffff' : '#1f2937',
      crosshairMarkerBackgroundColor: '#f59e0b',
      lineStyle: LineStyle.Dashed,
      title: 'SPY Buy & Hold',
    });
    spySeriesRef.current = spyLine;

    // Handle resize
    const handleResize = () => {
      if (!chartContainerRef.current || !chartRef.current) return;
      chartRef.current.applyOptions({
        width: chartContainerRef.current.clientWidth,
        height: chartContainerRef.current.clientHeight,
      });
    };
    window.addEventListener('resize', handleResize);

    applyTheme();

    // Reapply theme when dark mode changes
    const themeObserver = new MutationObserver(() => {
      setIsDarkMode(document.documentElement.classList.contains('dark'));
      applyTheme();
    });
    themeObserver.observe(document.documentElement, {
      attributes: true,
      attributeFilter: ['class'],
    });

    return () => {
      themeObserver.disconnect();
      window.removeEventListener('resize', handleResize);
      if (chartRef.current) { 
        chartRef.current.remove(); 
        chartRef.current = null; 
      }
      holdSeriesRef.current = null;
      highSeriesRef.current = null;
      spySeriesRef.current = null;
    };
  }, [data?.tradingData]);

  // Update chart data when results change
  useEffect(() => {
    if (!chartRef.current || !holdSeriesRef.current || !highSeriesRef.current) return;
    
    const { holdData, highData } = getChartData();
    const spyDataPoints = getSpyPerformanceData();
    
    if (holdData.length > 0) {
      holdSeriesRef.current.setData(holdData);
    } else {
      holdSeriesRef.current.setData([]);
    }

    if (highData.length > 0) {
      highSeriesRef.current.setData(highData);
    } else {
      highSeriesRef.current.setData([]);
    }

    if (spySeriesRef.current) {
      if (spyDataPoints.length > 0) {
        spySeriesRef.current.setData(spyDataPoints);
      } else {
        spySeriesRef.current.setData([]);
      }
    }

    // Fit content after data update
    if (holdData.length > 0 || highData.length > 0 || spyDataPoints.length > 0) {
      chartRef.current.timeScale().fitContent();
    }
  }, [getChartData, getSpyPerformanceData, isDarkMode]);

  if (isLoading) {
    return (
      <div className="min-h-screen bg-background text-foreground p-4 md:p-8 pt-20 md:pt-8">
        <div className="max-w-7xl mx-auto">
          <Card className="p-8 bg-card border border-border text-center">
            <div className="text-sm uppercase tracking-widest text-muted-foreground mb-2">Loading</div>
            <div className="text-lg font-mono text-primary dark:text-cyan-400">Fetching backtest details…</div>
          </Card>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="min-h-screen bg-background text-foreground p-4 md:p-8 pt-20 md:pt-8">
        <div className="max-w-7xl mx-auto space-y-6">
          <div className="flex items-center gap-4">
            <Link to="/backtest">
              <Button 
                variant="outline" 
                size="sm"
                className="bg-background border border-border text-primary hover:border-primary hover:text-primary/80"
              >
                <ArrowLeft className="h-4 w-4 mr-1" />
                Back
              </Button>
            </Link>
            <h1 className="text-xl font-mono font-bold uppercase tracking-wider text-foreground"># Backtest Details</h1>
          </div>
          <Card className="p-6 bg-destructive/10 border border-destructive/30">
            <div className="text-sm font-mono text-destructive">{error}</div>
          </Card>
        </div>
      </div>
    );
  }

  if (!data) {
    return (
      <div className="min-h-screen bg-background text-foreground p-4 md:p-8 pt-20 md:pt-8">
        <div className="max-w-7xl mx-auto">
          <Card className="p-8 bg-card border border-border text-center">
            <div className="text-lg font-mono text-primary dark:text-cyan-400">No backtest data found.</div>
          </Card>
        </div>
      </div>
    );
  }

  const { tradingData, backtestEntry, isProcessing } = data;

  return (
    <div className="min-h-screen bg-background text-foreground p-4 md:p-8 pt-20 md:pt-8">
      <div className="max-w-[1600px] mx-auto space-y-6">
        <div className="flex flex-wrap items-center gap-4 border-b border-border pb-4">
          <Link to="/backtest">
            <Button 
              variant="outline" 
              size="sm"
              className="bg-background border border-border text-foreground hover:text-primary dark:hover:text-cyan-400 hover:border-primary dark:hover:border-cyan-700"
            >
              <ArrowLeft className="h-4 w-4 mr-1" />
              Back
            </Button>
          </Link>
          <h1 className="text-xl font-mono font-bold uppercase tracking-wider text-foreground"># Backtest Details</h1>
          {isProcessing && (
            <div className="flex items-center gap-2 px-3 py-1 bg-yellow-500/10 text-yellow-600 dark:text-yellow-400 border border-yellow-500/30 rounded-full text-xs font-mono uppercase tracking-wide">
              <Clock className="w-4 h-4" />
              Processing
            </div>
          )}
          
          {/* Dropdown Menu */}
          <div className="ml-auto">
            <DropdownMenu>
              <DropdownMenuTrigger>
                <Button
                  variant="outline"
                  size="sm"
                  className="bg-background border border-border text-muted-foreground hover:text-primary dark:hover:text-cyan-400 hover:border-primary dark:hover:border-cyan-700"
                >
                  <MoreVertical className="h-4 w-4" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent className="bg-popover border border-border text-popover-foreground">
                <DropdownMenuItem 
                  onClick={handleCopyBacktest}
                  className="focus:bg-accent focus:text-accent-foreground"
                >
                  <Copy className="h-4 w-4 mr-2" />
                  Copy
                </DropdownMenuItem>
                <DropdownMenuItem 
                  onClick={handleCreateStrategy}
                  className="focus:bg-accent focus:text-accent-foreground"
                >
                  <Bot className="h-4 w-4 mr-2" />
                  Create Strategy
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </div>

        {/* Processing Status Banner */}
        {isProcessing && (
          <Card className="bg-yellow-500/10 border border-yellow-500/30 p-4">
            <div className="flex flex-wrap items-center gap-4 justify-between">
              <div className="flex items-start gap-3">
                <div className="animate-spin mt-1">
                  <RefreshCw className="w-5 h-5 text-yellow-600 dark:text-yellow-400" />
                </div>
                <div className="space-y-1">
                  <h3 className="text-sm font-mono uppercase tracking-wide text-yellow-600 dark:text-yellow-400">Backtest Processing</h3>
                  <p className="text-sm text-yellow-700 dark:text-yellow-300">
                    Your backtest is running. This typically takes 2-5 minutes. Results will appear automatically when complete.
                  </p>
                </div>
              </div>
              <Button
                variant="outline"
                size="sm"
                onClick={handleRefreshResults}
                className="bg-yellow-500/10 border border-yellow-500/30 text-yellow-600 dark:text-yellow-400 hover:text-yellow-700 dark:hover:text-yellow-300 hover:border-yellow-500/50"
              >
                <RefreshCw className="w-4 h-4 mr-1" />
                Check Now
              </Button>
            </div>
          </Card>
        )}

        {/* Two Column Layout: Main Content + Sidebar */}
        <div className="grid grid-cols-1 lg:grid-cols-[1fr_320px] gap-6">
          {/* Main Content Column */}
          <div className="space-y-6">
            {/* Results Section */}
            {tradingData ? (
              <div className="space-y-6">
            {/* Strategy Cards - Show filtered statistics */}
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
              <BacktestStrategyCard 
                title={`Hold Strategy${hasActiveFilters ? ' (Filtered)' : ''}`}
                strategy={hasActiveFilters ? filteredStrategyStats.hold : allStrategyStats.hold} 
                tradingData={{ ...tradingData, results: calculatedDailyResults }}
                strategyKey="hold"
              />
              <BacktestStrategyCard 
                title={`High Strategy${hasActiveFilters ? ' (Filtered)' : ''}`}
                strategy={hasActiveFilters ? filteredStrategyStats.high : allStrategyStats.high} 
                tradingData={{ ...tradingData, results: calculatedDailyResults }}
                strategyKey="high"
              />
              {(allStrategyStats.other.sumProfit !== 0 || filteredStrategyStats.other.sumProfit !== 0) && (
                <BacktestStrategyCard 
                  title={`Other Strategy${hasActiveFilters ? ' (Filtered)' : ''}`}
                  strategy={hasActiveFilters ? filteredStrategyStats.other : allStrategyStats.other} 
                  tradingData={{ ...tradingData, results: calculatedDailyResults }}
                  strategyKey="other"
                />
              )}
            </div>

            {/* Chart View */}
            <Card className="bg-card border border-border p-4 md:p-6">
              <div className="mb-4 border-b border-border pb-3">
                <div className="flex items-center justify-between mb-2">
                  <h3 className="text-xs font-mono uppercase tracking-wider text-primary dark:text-cyan-400"># Balance Over Time</h3>
                  {hasActiveFilters && (
                    <div className="flex items-center gap-2 text-[10px] font-mono text-muted-foreground">
                      <TrendingUp className="h-3 w-3 text-primary dark:text-cyan-400" />
                      <span className="text-primary dark:text-cyan-400">
                        {filteredTradingEntries.length} of {data?.tradingData?.entries?.length || 0} entries
                      </span>
                    </div>
                  )}
                </div>
                <div className="flex items-center gap-4 flex-wrap">
                  {data?.backtestEntry && (
                    <p className="text-[10px] font-mono text-muted-foreground">
                      {'>> '}INIT BAL: {formatPrice(getRequestData(data.backtestEntry).positionInfo.startingBalance || 10000)}
                    </p>
                  )}
                  <div className="flex items-center gap-2">
                    <div className="flex items-center gap-1.5">
                      <div className="w-3 h-0.5 bg-[#8b5cf6]"></div>
                      <span className="text-[10px] font-mono text-muted-foreground">Hold Strategy</span>
                    </div>
                    <div className="flex items-center gap-1.5">
                      <div className="w-3 h-0.5 bg-[#16a34a]"></div>
                      <span className="text-[10px] font-mono text-muted-foreground">High Strategy</span>
                    </div>
                    <div className="flex items-center gap-1.5">
                      <div className="w-3 h-0.5 bg-[#f59e0b] border-dashed border-t-2"></div>
                      <span className="text-[10px] font-mono text-muted-foreground">SPY Buy & Hold</span>
                    </div>
                  </div>
                </div>
              </div>
              
              <div ref={chartContainerRef} className="w-full h-[600px] bg-white dark:bg-[#0a0e17] border border-border" />
            </Card>
              </div>
            ) : isProcessing ? (
          <Card className="bg-card border border-border p-12 text-center space-y-4">
            <div className="animate-spin mx-auto w-8 h-8">
              <RefreshCw className="w-8 h-8 text-primary dark:text-cyan-400" />
            </div>
            <div className="space-y-2">
              <h3 className="text-sm font-mono uppercase tracking-wide text-primary dark:text-cyan-400">Processing Backtest Results</h3>
              <p className="text-xs text-muted-foreground max-w-xl mx-auto">
                Your backtest is executing. Charts and performance metrics will appear here automatically once processing completes.
              </p>
            </div>
            <div className="text-[10px] font-mono uppercase tracking-wide text-muted-foreground">
              Estimated completion · 2-5 minutes
            </div>
          </Card>
            ) : (
              <Card className="bg-card border border-border p-12 text-center space-y-2">
                <h3 className="text-sm font-mono uppercase tracking-wide text-foreground">No Results Available</h3>
                <p className="text-xs text-muted-foreground">
                  Results for this backtest are not currently available.
                </p>
              </Card>
            )}
          </div>

          {/* Sidebar Column */}
          {backtestEntry && (
            <div className="space-y-4 sticky top-4">
              <Card className="bg-card border border-border p-3">
                <h2 className="text-xs font-mono uppercase tracking-wider text-primary dark:text-cyan-400 mb-3"># Configuration</h2>
                
                {/* Backtest Info */}
                <div className="mb-4 pb-3 border-b border-border">
                  <h3 className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground mb-2"># Backtest Info</h3>
                  <div className="space-y-1.5">
                    <div className="flex items-center justify-between">
                      <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">STATUS</span>
                      <span className={`px-1.5 py-0.5 rounded-full text-[9px] font-mono uppercase tracking-wide ${
                        backtestEntry.status === 'Completed' 
                          ? 'bg-green-500/10 text-green-600 dark:text-green-400 border border-green-500/30' :
                        backtestEntry.status === 'InProgress' 
                          ? 'bg-yellow-500/10 text-yellow-600 dark:text-yellow-400 border border-yellow-500/30' :
                          'bg-red-500/10 text-red-600 dark:text-red-400 border border-red-500/30'
                      }`}>
                        {backtestEntry.status}
                      </span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">CREDITS</span>
                      <span className="text-xs font-mono font-bold text-primary dark:text-cyan-400">{backtestEntry.creditsUsed?.toFixed(2) || 'N/A'}</span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">DURATION</span>
                      <span className="text-xs font-mono font-bold text-primary dark:text-cyan-400">{formatDuration((backtestEntry as any).durationSeconds)}</span>
                    </div>
                    <div className="flex flex-col gap-0.5 pt-1">
                      <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">CREATED</span>
                      <span className="text-[10px] font-mono text-primary dark:text-cyan-400 leading-tight">{new Date(backtestEntry.createdAt).toLocaleString()}</span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">START</span>
                      <span className="text-[10px] font-mono font-bold text-primary dark:text-cyan-400">{formatDateNoTimezone(backtestEntry.start)}</span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">END</span>
                      <span className="text-[10px] font-mono font-bold text-primary dark:text-cyan-400">{formatDateNoTimezone(backtestEntry.end)}</span>
                    </div>
                  </div>
                </div>

                {/* Entry Conditions */}
                {(() => {
                  const requestData = getRequestData(backtestEntry);
                  const hasEntryConditions = (requestData.filters && requestData.filters.length > 0) || requestData.argument;
                  
                  if (!hasEntryConditions) return null;
                  
                  return (
                    <div className="mb-4 pb-3 border-b border-border">
                      <h3 className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground mb-2"># Entry Conditions</h3>
                      {requestData.filters && requestData.filters.length > 0 ? (
                        <div className="space-y-1.5">
                          {requestData.filters.map((filter: string, index: number) => (
                            <div key={index} className="font-mono text-[10px] text-primary dark:text-cyan-400 px-2 py-1 bg-muted/30 dark:bg-gray-950/50 border border-border hover:border-primary dark:hover:border-cyan-700 transition-colors">
                              {'>> '}{filter}
                            </div>
                          ))}
                        </div>
                      ) : requestData.argument ? (
                        <div>
                          <FilterDisplay argument={requestData.argument} />
                        </div>
                      ) : null}
                    </div>
                  );
                })()}

                {/* Position Settings */}
                {(() => {
                  const requestData = getRequestData(backtestEntry);
                  const positionInfo = requestData.positionInfo;
                  return (
                    <div className="mb-4 pb-3 border-b border-border">
                      <h3 className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground mb-2"># Position Settings</h3>
                      <div className="space-y-1.5">
                        <div className="flex items-center justify-between">
                          <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">INIT BALANCE</span>
                          <span className="text-xs font-mono font-bold text-primary dark:text-cyan-400">${positionInfo.startingBalance?.toLocaleString() || 'N/A'}</span>
                        </div>
                        <div className="flex items-center justify-between">
                          <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">POS SIZE</span>
                          <span className="text-xs font-mono font-bold text-primary dark:text-cyan-400">${positionInfo.positionSize?.toLocaleString() || 'N/A'}</span>
                        </div>
                        <div className="flex items-center justify-between">
                          <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">MAX POS</span>
                          <span className="text-xs font-mono font-bold text-primary dark:text-cyan-400">{positionInfo.maxConcurrentPositions || 'N/A'}</span>
                        </div>
                        <div className="flex items-center justify-between">
                          <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">MODEL TYPE</span>
                          <span className="text-[10px] font-mono font-bold text-primary dark:text-cyan-400">{(positionInfo as any).modelType || 'Fixed'}</span>
                        </div>
                        {(positionInfo as any).allowSimultaneous !== undefined && (
                          <div className="flex items-center justify-between">
                            <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">SIMULTANEOUS</span>
                            <span className="text-[10px] font-mono font-bold text-primary dark:text-cyan-400">{(positionInfo as any).allowSimultaneous ? 'ENABLED' : 'DISABLED'}</span>
                          </div>
                        )}
                      </div>
                    </div>
                  );
                })()}

                {/* Exit Strategy */}
                {(() => {
                  const requestData = getRequestData(backtestEntry);
                  const exitInfo = requestData.exitInfo;
                  if (!exitInfo.stopLoss && !exitInfo.profitTarget && !exitInfo.timeframe && !(exitInfo as any).avoidOvernight) return null;
                  
                  return (
                    <div>
                      <h3 className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground mb-2"># Exit Strategy</h3>
                      <div className="space-y-1.5">
                        {exitInfo.stopLoss && (
                          <div className="flex items-center justify-between">
                            <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">STOP LOSS</span>
                            <span className="text-[10px] font-mono font-bold text-primary dark:text-cyan-400">{formatStopConfig(exitInfo.stopLoss)}</span>
                          </div>
                        )}
                        {exitInfo.profitTarget && (
                          <div className="flex items-center justify-between">
                            <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">TAKE PROFIT</span>
                            <span className="text-[10px] font-mono font-bold text-primary dark:text-cyan-400">{formatStopConfig(exitInfo.profitTarget)}</span>
                          </div>
                        )}
                        {exitInfo.timeframe && (
                          <div className="flex items-center justify-between">
                            <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">TIMED EXIT</span>
                            <span className="text-[10px] font-mono font-bold text-primary dark:text-cyan-400">{formatTimeframe(exitInfo.timeframe)}</span>
                          </div>
                        )}
                        {(exitInfo as any).avoidOvernight !== undefined && (
                          <div className="flex items-center justify-between">
                            <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">AVOID OVERNIGHT</span>
                            <span className="text-[10px] font-mono font-bold text-primary dark:text-cyan-400">{(exitInfo as any).avoidOvernight ? 'ENABLED' : 'DISABLED'}</span>
                          </div>
                        )}
                      </div>
                    </div>
                  );
                })()}
              </Card>

              {/* Global Filters */}
              {tradingData && (
                <Card className="bg-card border border-border p-3 space-y-3">
                  <div className="flex items-center justify-between">
                    <h3 className="text-xs font-mono uppercase tracking-wider text-primary dark:text-cyan-400">:: Filter Results</h3>
                    <Button 
                      variant="outline" 
                      size="sm"
                      onClick={clearAllFilters}
                      className={`bg-background border border-border text-muted-foreground hover:text-primary dark:hover:text-cyan-400 hover:border-primary dark:hover:border-cyan-700 text-xs h-7 px-2 ${hasActiveFilters ? '' : 'invisible'}`}
                    >
                      <X className="h-3 w-3 mr-1" />
                      Clear All
                    </Button>
                  </div>
                  
                  {/* Result Count */}
                  <div className="pb-2 border-b border-border">
                    <div className="text-[10px] font-mono uppercase tracking-wide text-muted-foreground">
                      <div className="font-semibold text-base text-primary dark:text-cyan-400">
                        {filteredTradeResults.length}
                      </div>
                      <div className="text-[9px] text-muted-foreground">
                        of {rawTradeResults.length} trades
                        {hasActiveFilters && (
                          <span className="text-primary dark:text-cyan-400 ml-1">(filtered)</span>
                        )}
                      </div>
                      <div className={`text-[9px] text-muted-foreground mt-0.5 ${hasActiveFilters ? '' : 'invisible'}`}>
                        From {filteredTradingEntries.length} of {data?.tradingData?.entries?.length || 0} entries
                      </div>
                    </div>
                  </div>

                  {/* Search */}
                  <div>
                    <label className="block text-[9px] font-mono uppercase text-muted-foreground mb-1">
                      Search Ticker
                    </label>
                    <input
                      type="text"
                      value={globalFilters.searchTerm}
                      onChange={(e) => updateFilters({ searchTerm: e.target.value })}
                      placeholder="Search by ticker..."
                      className="w-full px-2 py-1.5 bg-card border border-border text-foreground placeholder:text-muted-foreground text-xs font-mono rounded-md focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary h-8"
                    />
                  </div>

                  {/* Day of Week */}
                  <div>
                    <label className="block text-[9px] font-mono uppercase text-muted-foreground mb-1">
                      Day of Week
                    </label>
                    <div className="flex flex-wrap gap-1">
                      {(['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'] as DayOfWeek[]).map(day => (
                        <button
                          key={day}
                          onClick={() => {
                            const newDays = globalFilters.dayOfWeek.includes(day)
                              ? globalFilters.dayOfWeek.filter(d => d !== day)
                              : [...globalFilters.dayOfWeek, day];
                            updateFilters({ dayOfWeek: newDays });
                          }}
                          className={`px-1.5 py-0.5 text-[10px] rounded-md border font-mono uppercase tracking-wide transition-colors ${
                            globalFilters.dayOfWeek.includes(day)
                              ? 'bg-primary/10 dark:bg-cyan-950/50 text-primary dark:text-cyan-400 border-primary dark:border-cyan-700'
                              : 'bg-muted/50 text-muted-foreground border-border hover:border-primary dark:hover:border-cyan-700 hover:text-primary dark:hover:text-cyan-400'
                          }`}
                        >
                          {day.slice(0, 3)}
                        </button>
                      ))}
                    </div>
                  </div>

                  {/* Time Range */}
                  <div>
                    <label className="block text-[9px] font-mono uppercase text-muted-foreground mb-1">
                      Time Range (Entry Time)
                    </label>
                    <div className="flex gap-2">
                      <input
                        type="time"
                        value={globalFilters.timeRange?.start || ''}
                        onChange={(e) => {
                          const start = e.target.value;
                          const end = globalFilters.timeRange?.end || start;
                          updateFilters({ 
                            timeRange: start ? { start, end } : null 
                          });
                        }}
                        className="flex-1 px-2 py-1.5 bg-card border border-border text-foreground text-xs font-mono rounded-md focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary h-8"
                      />
                      <span className="text-muted-foreground self-center text-[10px] font-mono uppercase">to</span>
                      <input
                        type="time"
                        value={globalFilters.timeRange?.end || ''}
                        onChange={(e) => {
                          const end = e.target.value;
                          const start = globalFilters.timeRange?.start || end;
                          updateFilters({ 
                            timeRange: end ? { start, end } : null 
                          });
                        }}
                        className="flex-1 px-2 py-1.5 bg-card border border-border text-foreground text-xs font-mono rounded-md focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary h-8"
                      />
                    </div>
                  </div>

                  {/* Exclude Tickers */}
                  <div>
                    <label className="block text-[9px] font-mono uppercase text-muted-foreground mb-1">
                      Exclude Tickers
                    </label>
                    <input
                      type="text"
                      placeholder="e.g., AAPL, MSFT, GOOGL"
                      value={globalFilters.excludeTickers.join(', ')}
                      onChange={(e) => {
                        const tickers = e.target.value
                          .split(',')
                          .map(ticker => ticker.trim().toUpperCase())
                          .filter(ticker => ticker.length > 0);
                        updateFilters({ excludeTickers: tickers });
                      }}
                      className="w-full px-2 py-1.5 bg-card border border-border text-foreground placeholder:text-muted-foreground/60 text-xs font-mono rounded-md focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary h-8"
                    />
                  </div>

                  {/* Active Filter Chips - Always reserve space */}
                  <div className="mt-3 pt-3 border-t border-border min-h-[24px]">
                    {hasActiveFilters && (
                      <div className="flex flex-wrap gap-1.5">
                      {globalFilters.searchTerm && (
                        <div className="flex items-center gap-1 px-2 py-0.5 bg-primary/10 border border-primary/30 text-primary rounded-full text-[10px] font-mono">
                          <span>Ticker: "{globalFilters.searchTerm}"</span>
                            <button 
                              onClick={() => updateFilters({ searchTerm: '' })}
                            className="ml-1 hover:bg-primary/20 rounded-full p-0.5 text-primary"
                            >
                              <X className="h-2.5 w-2.5" />
                            </button>
                          </div>
                        )}
                        
                        {globalFilters.dayOfWeek.length > 0 && (
                        <div className="flex items-center gap-1 px-2 py-0.5 bg-primary/10 dark:bg-cyan-950/50 border border-primary/30 dark:border-cyan-800/50 text-primary dark:text-cyan-400 rounded-full text-[10px] font-mono">
                            <span>Days: {globalFilters.dayOfWeek.join(', ')}</span>
                            <button 
                              onClick={() => updateFilters({ dayOfWeek: [] })}
                            className="ml-1 hover:bg-primary/20 dark:hover:bg-cyan-900/30 rounded-full p-0.5 text-primary dark:text-cyan-400"
                            >
                              <X className="h-2.5 w-2.5" />
                            </button>
                          </div>
                        )}
                        
                        {globalFilters.timeRange && (
                        <div className="flex items-center gap-1 px-2 py-0.5 bg-primary/10 border border-primary/30 text-primary rounded-full text-[10px] font-mono">
                            <span>Time: {globalFilters.timeRange.start} to {globalFilters.timeRange.end}</span>
                            <button 
                              onClick={() => updateFilters({ timeRange: null })}
                            className="ml-1 hover:bg-primary/20 rounded-full p-0.5 text-primary"
                            >
                              <X className="h-2.5 w-2.5" />
                            </button>
                          </div>
                        )}
                        
                        {globalFilters.excludeTickers.length > 0 && (
                        <div className="flex items-center gap-1 px-2 py-0.5 bg-destructive/10 border border-destructive/30 text-destructive rounded-full text-[10px] font-mono">
                            <span>Excluded: {globalFilters.excludeTickers.join(', ')}</span>
                            <button 
                              onClick={() => updateFilters({ excludeTickers: [] })}
                            className="ml-1 hover:bg-destructive/20 rounded-full p-0.5 text-destructive"
                            >
                              <X className="h-2.5 w-2.5" />
                            </button>
                          </div>
                        )}
                      </div>
                    )}
                  </div>
                </Card>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
} 