import { getAuthHeaders } from '../../api/authToken';
import { useState, useRef, useEffect, useCallback } from 'react';
import { useParams, useNavigate, useLocation } from 'react-router-dom';
import { useQuery, useMutation } from '@tanstack/react-query';
import { Trade } from '../../types/trade';
import { TradeStatistics } from '../../components/trades/TradeStatistics';
import { TradesTable } from '../../components/trades/TradesTable';
import { Button } from '../../components/ui/button';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle, AlertDialogTrigger } from '../../components/ui/alert-dialog';
import { ToggleGroup, ToggleGroupItem } from '../../components/ui/toggle-group';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '../../components/ui/dropdown-menu';
import { ArrowLeft, Trash2, ChevronLeft, ChevronRight, MoreVertical, Copy, Settings, Beaker } from 'lucide-react';
import { toast } from '../../hooks/use-toast';
import { strategyApi } from '../../api/strategyApi';
import { Badge } from '../../components/ui/badge';
import { Card } from '../../components/ui/card';
import { formatPrice } from '../../utils/chartUtils';
import { createChart, LineSeries, ColorType, LineStyle, type IChartApi, type ISeriesApi } from 'lightweight-charts';
import type { LogicalRange } from 'lightweight-charts';
import { fetchMarketData } from '../../services/polygon';
import { StockMarketData } from '../../types/tools';
import { StrategyStatePanel } from '../../components/strategy/StrategyStatePanel';
import { BalanceHistoryChart } from '../../components/strategy/BalanceHistoryChart';

// Local type to reflect trade API response
type TradeResponse = {
  totalTrades: number;
  totalProfit: number;
  averageProfit: number;
  winRate: number;
  maxConcurrentTrades: number;
  trades: Trade[];
};

const StrategyDetailPage = () => {
  const { strategyId } = useParams<{ strategyId: string }>();
  const navigate = useNavigate();
  const location = useLocation();
  
  const [sortConfig, setSortConfig] = useState<{
    key: keyof Trade | null;
    direction: 'asc' | 'desc';
  }>({
    key: 'openedAt',
    direction: 'desc'
  });

  const [tradeType, setTradeType] = useState<string>('all');
  const [currentDate, setCurrentDate] = useState(new Date());
  const [selectedDate, setSelectedDate] = useState<Date | undefined>(undefined);
  const chartContainerRef = useRef<HTMLDivElement>(null);
  const chartRef = useRef<IChartApi | null>(null);
  const lineSeriesRef = useRef<ISeriesApi<'Line'> | null>(null);
  const spySeriesRef = useRef<ISeriesApi<'Line'> | null>(null);
  const baselineSeriesRef = useRef<ISeriesApi<'Line'> | null>(null);
  const hasUserInteractedRef = useRef(false);
  const lastVisibleLogicalRangeRef = useRef<LogicalRange | null>(null);
  const hasInitialFitRef = useRef(false);
  const tradeMapRef = useRef<Map<number, Date>>(new Map());
  const dateTradeMapRef = useRef<Map<string, Trade[]>>(new Map());
  const [spyData, setSpyData] = useState<StockMarketData | null>(null);
  const [isDarkMode, setIsDarkMode] = useState(() => 
    document.documentElement.classList.contains('dark')
  );
  
  // Pagination state
  const [currentPage, setCurrentPage] = useState(1);
  const [itemsPerPage, setItemsPerPage] = useState(10);

  // Fetch trading bot details
  const { data: strategy, isLoading: isLoadingStrategy } = useQuery({
    queryKey: ['strategy', strategyId],
    queryFn: () => strategyApi.getStrategy(strategyId!),
    enabled: !!strategyId,
  });

  // Fetch trades for this specific bot
  const { data: tradesResponse, error: tradesError } = useQuery({
    queryKey: ['strategyTrades', strategyId],
    queryFn: async () => {
      const response = await fetch(`https://stockmountain.io/api/trade?strategy=${strategyId}`, {
        headers: await getAuthHeaders()
      });
      if (!response.ok) {
        throw new Error('Network response was not ok');
      }
      const raw = await response.json();
      const normalized: TradeResponse = {
        totalTrades: raw?.totalTrades ?? raw?.TotalTrades ?? 0,
        totalProfit: raw?.totalProfit ?? raw?.TotalProfit ?? 0,
        averageProfit: raw?.averageProfit ?? raw?.AverageProfit ?? 0,
        winRate: raw?.winRate ?? raw?.WinRate ?? 0,
        maxConcurrentTrades: raw?.maxConcurrentTrades ?? raw?.MaxConcurrentTrades ?? 0,
        trades: (raw?.trades ?? raw?.Trades ?? []) as Trade[],
      };
      return normalized;
    },
    enabled: !!strategyId,
    refetchInterval: 30000,
  });

  // Fetch SPY data for comparison
  const { data: spyDataResponse } = useQuery({
    queryKey: ['spyData', strategyId, tradesResponse?.trades],
    queryFn: async () => {
      if (!tradesResponse?.trades || tradesResponse.trades.length === 0) {
        return null;
      }

      // Find date range from trades
      const sortedTrades = [...tradesResponse.trades]
        .filter(trade => trade.closedAt)
        .sort((a, b) => new Date(a.closedAt).getTime() - new Date(b.closedAt).getTime());

      if (sortedTrades.length === 0) {
        return null;
      }

      const firstTradeDate = new Date(sortedTrades[0].openedAt);
      const lastTradeDate = new Date(sortedTrades[sortedTrades.length - 1].closedAt);
      
      // Start on the first trade date, end with last trade or today
      const startDate = new Date(firstTradeDate);
      startDate.setHours(0, 0, 0, 0);
      
      const endDate = new Date(Math.max(lastTradeDate.getTime(), Date.now()));
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
    enabled: !!strategyId && !!tradesResponse?.trades && tradesResponse.trades.length > 0,
  });

  useEffect(() => {
    if (spyDataResponse) {
      setSpyData(spyDataResponse);
    }
  }, [spyDataResponse]);

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


  const deleteStrategyMutation = useMutation({
    mutationFn: strategyApi.deleteStrategy,
    onSuccess: () => {
      toast({
        title: "Success",
        description: "Strategy deleted successfully",
      });
      // Navigate back to dashboard after deletion
      window.location.href = '/optimus/dashboard';
    },
    onError: () => {
      toast({
        title: "Error",
        description: "Failed to delete strategy",
        variant: "destructive",
      });
    }
  });


  const handleDeleteStrategy = () => {
    if (strategyId) {
      deleteStrategyMutation.mutate(strategyId);
    }
  };

  const handleCloneStrategy = () => {
    if (!strategy) return;
    
    // Navigate to the new strategy editor with clone data
    navigate('/optimus/strategy/new', {
      state: {
        initialData: {
          ...strategy,
          id: undefined, // Will be generated by the API
          name: `${strategy.name} (Copy)`,
          state: 'Inactive', // Start inactive by default
        }
      }
    });
  };

  const trades = tradesResponse?.trades || [];
  const winRatePercent = typeof tradesResponse?.winRate === 'number'
    ? (tradesResponse.winRate > 1 ? tradesResponse.winRate : tradesResponse.winRate * 100)
    : undefined;
  const averageProfitValue = typeof tradesResponse?.averageProfit === 'number'
    ? tradesResponse.averageProfit
    : undefined;

  // Use new structure if available, fallback to legacy structure
  const positionSettings = strategy?.positionSettings || (strategy?.positionInfo ? {
    startingBalance: strategy.positionInfo.startingBalance,
    allowSimultaneous: (strategy.positionInfo.maxConcurrentPositions || 1) > 1,
    maxConcurrentPositions: strategy.positionInfo.maxConcurrentPositions || 1,
    model: {
      type: 'Fixed' as const,
      size: strategy.positionInfo.positionSize || 100
    }
  } : {
    startingBalance: 1000,
    allowSimultaneous: false,
    maxConcurrentPositions: 1,
    model: {
      type: 'Fixed' as const,
      size: 100
    }
  });

  const exitSettings = strategy?.exitSettings || (strategy?.exitInfo ? {
    stopLoss: strategy.exitInfo.stopLoss ? {
      candleType: 'PreviousCandle',
      type: strategy.exitInfo.stopLoss.type,
      value: strategy.exitInfo.stopLoss.value,
      priceActionType: strategy.exitInfo.stopLoss.priceActionType
    } : undefined,
    takeProfit: strategy.exitInfo.profitTarget ? {
      candleType: 'PreviousCandle',
      type: strategy.exitInfo.profitTarget.type,
      value: strategy.exitInfo.profitTarget.value,
      priceActionType: strategy.exitInfo.profitTarget.priceActionType
    } : undefined,
    timedExit: strategy.exitInfo.timeframe ? {
      timeframe: strategy.exitInfo.timeframe
    } : undefined
  } : {});

  // Determine the correct back navigation path
  const getBackNavigationPath = () => {
    // Check if we have state indicating where we came from
    if (location.state?.from) {
      return location.state.from;
    }

    // Check referrer to determine origin
    const referrer = document.referrer;
    if (referrer.includes('/optimus/public-dashboard')) {
      return '/optimus/public-dashboard';
    } else if (referrer.includes('/optimus/dashboard')) {
      return '/optimus/dashboard';
    }

    // Default fallback
    return '/optimus/dashboard';
  };

  const filteredTrades = trades.filter((trade: Trade) => {
    if (tradeType === 'all') return true;
    return trade.type.toLowerCase() === tradeType;
  });

  const sortData = (key: keyof Trade) => {
    const direction = sortConfig.key === key && sortConfig.direction === 'asc' ? 'desc' : 'asc';
    setSortConfig({ key, direction });
  };

  const getSortedData = () => {
    if (!sortConfig.key) return filteredTrades;

    return [...filteredTrades].sort((a, b) => {
      if (a[sortConfig.key!] === null) return 1;
      if (b[sortConfig.key!] === null) return -1;

      let aValue = a[sortConfig.key!];
      let bValue = b[sortConfig.key!];

      if (sortConfig.key === 'openedAt' || sortConfig.key === 'closedAt') {
        aValue = new Date(aValue as string).getTime();
        bValue = new Date(bValue as string).getTime();
      }

      if (aValue < bValue) return sortConfig.direction === 'asc' ? -1 : 1;
      if (aValue > bValue) return sortConfig.direction === 'asc' ? 1 : -1;
      return 0;
    });
  };

  // Get paginated data for table view
  const getPaginatedData = () => {
    const sortedData = getSortedData();
    const startIndex = (currentPage - 1) * itemsPerPage;
    const endIndex = startIndex + itemsPerPage;
    return sortedData.slice(startIndex, endIndex);
  };

  // Pagination helpers
  const totalPages = Math.ceil(filteredTrades.length / itemsPerPage);
  const hasNextPage = currentPage < totalPages;
  const hasPrevPage = currentPage > 1;

  // Reset page when filters change
  const handleTradeTypeChange = (value: string) => {
    setTradeType(value || 'all');
    setCurrentPage(1); // Reset to first page when filter changes
  };

  // Calendar helper functions
  const daysOfWeek = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
  const currentYear = currentDate.getFullYear();
  const currentMonth = currentDate.getMonth();

  const calendarData = (() => {
    const firstDay = new Date(currentYear, currentMonth, 1);
    const lastDay = new Date(currentYear, currentMonth + 1, 0);
    const firstDayOfWeek = firstDay.getDay();
    const daysInMonth = lastDay.getDate();
    
    const days = Array(firstDayOfWeek).fill(null).concat(
      Array.from({ length: daysInMonth }, (_, i) => i + 1)
    );
    
    const rows = [];
    let cells = [];
    
    days.forEach((day, i) => {
      if (i > 0 && i % 7 === 0) {
        rows.push(cells);
        cells = [];
      }
      cells.push(day);
    });
    
    while (cells.length < 7) {
      cells.push(null);
    }
    rows.push(cells);
    
    return rows;
  })();

  const getDayProfit = (date: Date) => {
    return filteredTrades.reduce((total, trade) => {
      const tradeDate = new Date(trade.closedAt);
      if (
        tradeDate.getDate() === date.getDate() &&
        tradeDate.getMonth() === date.getMonth() &&
        tradeDate.getFullYear() === date.getFullYear()
      ) {
        return total + (trade.profit || 0);
      }
      return total;
    }, 0);
  };

  const getDayTrades = (date: Date): Trade[] => {
    return filteredTrades.filter(trade => {
      const tradeDate = new Date(trade.closedAt);
      return (
        tradeDate.getDate() === date.getDate() &&
        tradeDate.getMonth() === date.getMonth() &&
        tradeDate.getFullYear() === date.getFullYear()
      );
    });
  };

  const getDayColor = (day: number | null) => {
    if (day === null) return '';
    
    const date = new Date(currentYear, currentMonth, day);
    const profit = getDayProfit(date);
    
    if (profit > 0) return 'bg-green-100/50 dark:bg-green-950/50 hover:bg-green-200 dark:hover:bg-green-950 border-green-300 dark:border-green-800 text-green-700 dark:text-green-400';
    if (profit < 0) return 'bg-red-100/50 dark:bg-red-950/50 hover:bg-red-200 dark:hover:bg-red-950 border-red-300 dark:border-red-800 text-red-700 dark:text-red-400';
    return 'hover:bg-muted';
  };

  const isSelected = (day: number | null) => {
    if (!day || !selectedDate) return false;
    
    return (
      selectedDate.getDate() === day &&
      selectedDate.getMonth() === currentMonth &&
      selectedDate.getFullYear() === currentYear
    );
  };

  const isToday = (day: number | null) => {
    if (day === null) return false;
    
    const today = new Date();
    return (
      today.getDate() === day &&
      today.getMonth() === currentMonth &&
      today.getFullYear() === currentYear
    );
  };

  const handleDayClick = (day: number | null) => {
    if (day === null) return;
    const selectedDate = new Date(currentYear, currentMonth, day);
    setSelectedDate(selectedDate);
  };

  const goToPreviousMonth = () => {
    setCurrentDate(new Date(currentYear, currentMonth - 1, 1));
  };

  const goToNextMonth = () => {
    setCurrentDate(new Date(currentYear, currentMonth + 1, 1));
  };

  const monthYearDisplay = currentDate.toLocaleDateString('en-US', {
    month: 'long',
    year: 'numeric'
  });

  const selectedDayTrades = selectedDate ? getDayTrades(selectedDate) : [];
  const selectedDayProfit = selectedDate ? getDayProfit(selectedDate) : 0;

  const handleTradeClick = (trade: Trade) => {
    navigate(`/optimus/trade/${trade.id}`, { state: { trade } });
  };

  // Calculate cumulative balance data for chart (aggregated by day)
  const getCumulativeBalanceData = useCallback(() => {
    if (!strategy || filteredTrades.length === 0) {
      return { balanceData: [], tradeMap: new Map(), dateTradeMap: new Map() };
    }

    const startingBalance = positionSettings.startingBalance;
    let cumulativeBalance = startingBalance;
    
    // Filter and sort trades by closed date
    const sortedTrades = [...filteredTrades]
      .filter(trade => trade.closedAt && trade.profit !== undefined)
      .sort((a, b) => new Date(a.closedAt).getTime() - new Date(b.closedAt).getTime());

    if (sortedTrades.length === 0) {
      return { balanceData: [], tradeMap: new Map(), dateTradeMap: new Map() };
    }

    // Group trades by day
    const tradesByDay = new Map<string, Trade[]>();
    sortedTrades.forEach(trade => {
      const tradeDate = new Date(trade.closedAt);
      const dayKey = `${tradeDate.getFullYear()}-${tradeDate.getMonth()}-${tradeDate.getDate()}`;
      if (!tradesByDay.has(dayKey)) {
        tradesByDay.set(dayKey, []);
      }
      tradesByDay.get(dayKey)!.push(trade);
    });

    // Create data points for each day
    const balanceData = [];
    const tradeMap = new Map<number, Date>();
    const dateTradeMap = new Map<string, Trade[]>();

    // Get sorted day keys
    const sortedDayKeys = Array.from(tradesByDay.keys()).sort((a, b) => {
      const [yearA, monthA, dayA] = a.split('-').map(Number);
      const [yearB, monthB, dayB] = b.split('-').map(Number);
      return new Date(yearA, monthA, dayA).getTime() - new Date(yearB, monthB, dayB).getTime();
    });

    // Add starting balance point on the first trade date (at start of day)
    if (sortedDayKeys.length > 0) {
      const firstDayKey = sortedDayKeys[0];
      const [year, month, day] = firstDayKey.split('-').map(Number);
      const firstDayDate = new Date(year, month, day);
      firstDayDate.setHours(0, 0, 0, 0);
      const firstDayTimestamp = Math.floor(firstDayDate.getTime() / 1000);
      
      balanceData.push({
        time: firstDayTimestamp,
        value: startingBalance
      });
      tradeMap.set(firstDayTimestamp, firstDayDate);
      dateTradeMap.set(firstDayKey, []);
    }

    // Process each day, adding end-of-day balance points
    sortedDayKeys.forEach(dayKey => {
      const [year, month, day] = dayKey.split('-').map(Number);
      const dayDate = new Date(year, month, day);
      dayDate.setHours(16, 0, 0, 0); // Use 4 PM (market close) for daily data points
      const dayTimestamp = Math.floor(dayDate.getTime() / 1000);

      const dayTrades = tradesByDay.get(dayKey)!;
      const dayProfit = dayTrades.reduce((sum, trade) => sum + trade.profit, 0);
      cumulativeBalance += dayProfit;

      balanceData.push({
        time: dayTimestamp,
        value: cumulativeBalance
      });
      tradeMap.set(dayTimestamp, dayDate);
      dateTradeMap.set(dayKey, dayTrades);
    });


    return { balanceData, tradeMap, dateTradeMap };
  }, [strategy, filteredTrades, positionSettings]);

  // Calculate SPY performance data
  const getSpyPerformanceData = useCallback(() => {
    if (!spyData || !spyData.results || spyData.results.length === 0 || !strategy || filteredTrades.length === 0) {
      return [];
    }

    const startingBalance = positionSettings.startingBalance;
    
    // Sort SPY data by timestamp
    const sortedSpyBars = [...spyData.results].sort((a, b) => a.t - b.t);
    
    if (sortedSpyBars.length === 0) {
      return [];
    }

    // Find the first trade date to match the strategy's starting point
    const sortedTrades = [...filteredTrades]
      .filter(trade => trade.closedAt)
      .sort((a, b) => new Date(a.closedAt).getTime() - new Date(b.closedAt).getTime());
    
    if (sortedTrades.length === 0) {
      return [];
    }

    const firstTradeDate = new Date(sortedTrades[0].openedAt);
    firstTradeDate.setHours(0, 0, 0, 0);
    const firstTradeTimestamp = Math.floor(firstTradeDate.getTime() / 1000);

    // Get the first SPY bar that corresponds to the first trade date or later
    // Use the first bar's close price to calculate initial shares
    const firstBar = sortedSpyBars[0];
    const initialPrice = firstBar.c;
    const initialShares = startingBalance / initialPrice;

    // Create SPY performance data points, starting with the starting balance on the first trade date
    const spyDataPoints = [];
    
    // Add starting point at the beginning of the first trade date
    spyDataPoints.push({
      time: firstTradeTimestamp,
      value: startingBalance
    });

    // Add data points for each SPY bar
    // Convert bar timestamps and compare dates (not exact timestamps, since bars might be at different times of day)
    sortedSpyBars.forEach(bar => {
      const portfolioValue = initialShares * bar.c;
      const barTimestamp = Math.floor(bar.t / 1000); // Convert milliseconds to seconds
      
      // Convert to date for comparison (ignore time of day)
      const barDate = new Date(barTimestamp * 1000);
      barDate.setHours(0, 0, 0, 0);
      const barDateTimestamp = Math.floor(barDate.getTime() / 1000);
      
      // Only add if the bar date is on or after the first trade date
      if (barDateTimestamp >= firstTradeTimestamp) {
        spyDataPoints.push({
          time: barTimestamp, // Use original timestamp for proper alignment
          value: portfolioValue
        });
      }
    });

    return spyDataPoints;
  }, [spyData, strategy, filteredTrades, positionSettings]);

  // Theme helpers
  const applyTheme = () => {
    if (!chartRef.current || !lineSeriesRef.current) return;
    const colors = isDarkMode ? {
      background: '#0b1220',
      text: '#e5e7eb',
      grid: '#1f2937',
      crosshair: '#6b7280',
      line: '#8b5cf6',
      markerBorder: '#8b5cf6',
      markerBg: '#0b1220',
    } : {
      background: '#ffffff',
      text: '#1f2937',
      grid: '#e5e7eb',
      crosshair: '#9ca3af',
      line: '#8b5cf6',
      markerBorder: '#8b5cf6',
      markerBg: '#ffffff',
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
    lineSeriesRef.current.applyOptions({
      color: colors.line,
      crosshairMarkerBorderColor: colors.markerBorder,
      crosshairMarkerBackgroundColor: colors.markerBg,
    });
    
    // Update SPY series colors
    if (spySeriesRef.current) {
      spySeriesRef.current.applyOptions({
        crosshairMarkerBorderColor: isDarkMode ? '#ffffff' : '#1f2937',
      });
    }
    
    // Update baseline series color
    if (baselineSeriesRef.current) {
      baselineSeriesRef.current.applyOptions({
        color: isDarkMode ? '#6b7280' : '#9ca3af',
      });
    }
  };

  // Create chart when trades become available (container might not exist initially)
  useEffect(() => {
    if (!chartContainerRef.current || !strategy) return;
    if (chartRef.current) return; // already created
    // Allow chart creation even if there are no trades yet (for SPY comparison)
    
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
      height: chartContainerRef.current.clientHeight || 400,
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

    const line = chart.addSeries(LineSeries, {
      color: '#8b5cf6',
      lineWidth: 3,
      crosshairMarkerVisible: true,
      crosshairMarkerRadius: 6,
      crosshairMarkerBorderColor: '#ffffff',
      crosshairMarkerBackgroundColor: '#8b5cf6',
      title: 'Strategy',
    });
    lineSeriesRef.current = line;

    // Add SPY comparison series
    const spyLine = chart.addSeries(LineSeries, {
      color: '#f59e0b', // Keep orange for SPY (works in both themes)
      lineWidth: 2,
      crosshairMarkerVisible: true,
      crosshairMarkerRadius: 5,
      crosshairMarkerBorderColor: initialIsDark ? '#ffffff' : '#1f2937',
      crosshairMarkerBackgroundColor: '#f59e0b',
      lineStyle: LineStyle.Dashed,
      title: 'SPY Buy & Hold',
    });
    spySeriesRef.current = spyLine;

    // Add baseline series for starting balance (dotted line)
    const baseline = chart.addSeries(LineSeries, {
      color: initialIsDark ? '#6b7280' : '#9ca3af',
      lineWidth: 1,
      lineStyle: LineStyle.Dotted,
      crosshairMarkerVisible: false,
    });
    baselineSeriesRef.current = baseline;

    // Track user pan/zoom
    const ts = chart.timeScale();
    ts.subscribeVisibleTimeRangeChange(() => {
      hasUserInteractedRef.current = true;
      const logical = ts.getVisibleLogicalRange();
      if (logical) lastVisibleLogicalRangeRef.current = logical as LogicalRange;
    });

    // Handle chart click
    const handleChartClick = (event: MouseEvent) => {
      if (!chartRef.current) return;
      const time = chartRef.current.timeScale().coordinateToTime(event.offsetX);
      if (!time) {
        setSelectedDate(undefined);
        return;
      }
      const timestamp = Number(time);
      const date = tradeMapRef.current.get(timestamp);
      if (date) {
        setSelectedDate(date);
      } else {
        // Find closest date
        let closestTimestamp: number | null = null;
        let minDiff = Infinity;
        for (const [ts] of tradeMapRef.current) {
          const diff = Math.abs(ts - timestamp);
          if (diff < minDiff) { minDiff = diff; closestTimestamp = ts; }
        }
        if (closestTimestamp && minDiff <= 86400) {
          setSelectedDate(tradeMapRef.current.get(closestTimestamp)!);
        } else {
          setSelectedDate(undefined);
        }
      }
    };
    chartContainerRef.current.addEventListener('click', handleChartClick);

    // Handle resize
    const handleResize = () => {
      if (!chartContainerRef.current || !chartRef.current) return;
      const ts = chartRef.current.timeScale();
      const logical = ts.getVisibleLogicalRange();
      chartRef.current.applyOptions({
        width: chartContainerRef.current.clientWidth,
        height: chartContainerRef.current.clientHeight,
      });
      if (logical) ts.setVisibleLogicalRange(logical as LogicalRange);
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
      if (chartContainerRef.current) chartContainerRef.current.removeEventListener('click', handleChartClick);
      if (chartRef.current) { chartRef.current.remove(); chartRef.current = null; }
      lineSeriesRef.current = null;
      spySeriesRef.current = null;
      baselineSeriesRef.current = null;
      hasInitialFitRef.current = false;
      hasUserInteractedRef.current = false;
    };
  }, [strategy, filteredTrades.length]);

  // Update data when trades change; preserve range
  useEffect(() => {
    if (!chartRef.current || !lineSeriesRef.current || !baselineSeriesRef.current || !strategy) return;
    
    const { balanceData, tradeMap, dateTradeMap } = getCumulativeBalanceData();
    const spyDataPoints = getSpyPerformanceData();
    
    // Allow update if we have either balance data or SPY data
    if (balanceData.length === 0 && spyDataPoints.length === 0) {
      // Clear all data if we have nothing to show
      if (lineSeriesRef.current) lineSeriesRef.current.setData([]);
      if (spySeriesRef.current) spySeriesRef.current.setData([]);
      if (baselineSeriesRef.current) baselineSeriesRef.current.setData([]);
      return;
    }

      const startingBalance = positionSettings.startingBalance;

    // Preserve logical range if user interacted
    const ts = chartRef.current.timeScale();
    const savedLogical = hasUserInteractedRef.current ? (ts.getVisibleLogicalRange() as LogicalRange | null) : null;

    try {
      // Update strategy balance data if available
      if (balanceData.length > 0) {
        lineSeriesRef.current.setData(balanceData);
        tradeMapRef.current = tradeMap;
        dateTradeMapRef.current = dateTradeMap;
      } else {
        // Clear strategy data if no trades
        lineSeriesRef.current.setData([]);
      }

      // Update SPY data if available
      if (spySeriesRef.current) {
        if (spyDataPoints.length > 0) {
          spySeriesRef.current.setData(spyDataPoints);
        } else {
          spySeriesRef.current.setData([]);
        }
      }

      // Create baseline data spanning the entire time range
      // Use the data that exists (prefer balance data, fallback to SPY)
      const timeRangeData = balanceData.length > 0 ? balanceData : spyDataPoints;
      const baselineData = timeRangeData.length > 0 
        ? [
            { time: timeRangeData[0].time, value: startingBalance },
            { time: timeRangeData[timeRangeData.length - 1].time, value: startingBalance }
          ]
        : [];
      baselineSeriesRef.current.setData(baselineData);

      if (savedLogical) {
        ts.setVisibleLogicalRange(savedLogical);
      } else if (!hasInitialFitRef.current) {
        ts.fitContent();
        hasInitialFitRef.current = true;
      }
    } catch (error) {
      console.error('Error setting chart data:', error);
    }
  }, [getCumulativeBalanceData, getSpyPerformanceData, strategy, positionSettings, isDarkMode]);

  if (isLoadingStrategy) {
    return (
      <div className="min-h-screen bg-background p-4 md:p-8">
        <div className="max-w-7xl mx-auto">
          <div className="text-center py-8 text-primary dark:text-cyan-400 font-mono text-sm">
            <div className="animate-pulse">Â» LOADING STRATEGY DATA...</div>
          </div>
        </div>
      </div>
    );
  }

  if (!strategy) {
    return (
      <div className="min-h-screen bg-background p-4 md:p-8">
        <div className="max-w-7xl mx-auto">
          <div className="text-center py-8 text-destructive dark:text-red-400 font-mono text-sm">
            <div>âš  STRATEGY NOT FOUND</div>
            <div className="text-muted-foreground text-xs mt-2">Â» Invalid strategy ID or access denied</div>
          </div>
        </div>
      </div>
    );
  }

  // Legacy exit for backward compatibility (used for display)
  const exit = strategy?.exitInfo || {
    stopLoss: exitSettings.stopLoss ? {
      priceActionType: exitSettings.stopLoss.priceActionType as 'open' | 'close' | 'high' | 'low',
      type: exitSettings.stopLoss.type,
      value: exitSettings.stopLoss.value
    } : undefined,
    profitTarget: exitSettings.takeProfit ? {
      priceActionType: exitSettings.takeProfit.priceActionType as 'open' | 'close' | 'high' | 'low',
      type: exitSettings.takeProfit.type,
      value: exitSettings.takeProfit.value
    } : undefined,
    timeframe: exitSettings.timedExit?.timeframe,
    other: undefined,
  };

  return (
    <div className="min-h-screen bg-background p-3 md:p-6 pt-20 md:pt-6">
      <div className="w-full space-y-4">
        {/* Header */}
        <div className="flex items-center justify-between border-b border-border pb-4">
          <div className="flex items-center gap-4">
            <Button
              variant="outline"
              size="sm"
              className="bg-card hover:bg-muted border-border hover:border-primary text-foreground hover:text-primary transition-all"
              onClick={() => navigate(getBackNavigationPath())}
            >
              <ArrowLeft className="h-4 w-4 mr-1" />
              <span className="font-mono text-xs uppercase">Back</span>
            </Button>
            <div>
              <h1 className="text-2xl font-bold text-foreground font-mono uppercase tracking-wider">{strategy.name || 'UNNAMED STRATEGY'}</h1>
              <div className="flex items-center space-x-2 mt-2">
                <Badge 
                  variant={strategy.type === 'Paper' ? 'default' : 'default'}
                  className={`px-2 py-0.5 text-xs font-mono uppercase border ${
                    strategy.type === 'Paper' 
                      ? 'bg-yellow-100 dark:bg-yellow-950 text-yellow-700 dark:text-yellow-400 border-yellow-300 dark:border-yellow-700' 
                      : 'bg-emerald-100 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-400 border-emerald-300 dark:border-emerald-700'
                  }`}
                >
                  <div className={`w-1.5 h-1.5 rounded-full mr-1.5 ${
                    strategy.type === 'Paper' ? 'bg-yellow-600 dark:bg-yellow-400 animate-pulse' : 'bg-emerald-600 dark:bg-emerald-400 animate-pulse'
                  }`} />
                  {strategy.type || 'Paper'}
                </Badge>
                
                <Badge 
                  variant="default"
                  className="px-2 py-0.5 text-xs font-mono uppercase bg-primary/10 dark:bg-cyan-950 text-primary dark:text-cyan-400 border border-primary/30 dark:border-cyan-700"
                >
                  <div className="w-1.5 h-1.5 rounded-full mr-1.5 bg-primary dark:bg-cyan-400" />
                  {strategy.integration || 'Default'}
                </Badge>
                
                {/* Status Badge */}
                <div className="flex items-center gap-3">
                  <Badge 
                    variant={strategy.state === 'active' ? 'default' : 'secondary'}
                    className={`px-2 py-0.5 text-xs font-mono uppercase border ${
                      strategy.state === 'active' 
                        ? 'bg-green-100 dark:bg-green-950 text-green-700 dark:text-green-400 border-green-300 dark:border-green-700' 
                        : 'bg-red-100 dark:bg-red-950 text-red-700 dark:text-red-400 border-red-300 dark:border-red-700'
                    }`}
                  >
                    <div className={`w-1.5 h-1.5 rounded-full mr-1.5 ${
                      strategy.state === 'active' ? 'bg-green-600 dark:bg-green-400 animate-pulse' : 'bg-red-600 dark:bg-red-400'
                    }`} />
                    {strategy.state === 'active' ? 'ACTIVE' : 'OFFLINE'}
                  </Badge>
                </div>
              </div>
            </div>
          </div>
          
          <div className="flex items-center gap-2">
            {/* Dropdown Menu */}
            <DropdownMenu>
              <DropdownMenuTrigger>
                <Button
                  variant="outline"
                  size="sm"
                  className="bg-card hover:bg-muted border-border hover:border-primary text-foreground hover:text-primary transition-all"
                >
                  <MoreVertical className="h-4 w-4" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent className="bg-card border-border">
                <DropdownMenuItem onClick={handleCloneStrategy} className="text-foreground hover:text-primary hover:bg-muted font-mono text-xs">
                  <Copy className="h-3 w-3 mr-2" />
                  CLONE
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>

            {/* Optimize Button */}
            <Button
              variant="outline"
              size="sm"
              className="bg-emerald-50 dark:bg-emerald-950 hover:bg-emerald-100 dark:hover:bg-emerald-900 border-emerald-300 dark:border-emerald-700 text-emerald-700 dark:text-emerald-400 hover:border-emerald-400 dark:hover:border-emerald-500 transition-all"
              onClick={() => navigate(`/optimus/strategy/${strategyId}/optimize`)}
            >
              <Beaker className="h-3 w-3 mr-1.5" />
              <span className="font-mono text-xs uppercase">Optimize</span>
            </Button>

            {/* Settings Button */}
            <Button
              variant="outline"
              size="sm"
              className="bg-card hover:bg-muted border-border hover:border-primary text-foreground hover:text-primary transition-all"
              onClick={() => navigate(`/optimus/strategy/${strategyId}/settings`)}
            >
              <Settings className="h-3 w-3 mr-1.5" />
              <span className="font-mono text-xs uppercase">Config</span>
            </Button>

            <AlertDialog>
              <AlertDialogTrigger asChild>
                <Button 
                  variant="destructive" 
                  size="sm"
                  className="bg-red-100 dark:bg-red-950 hover:bg-red-200 dark:hover:bg-red-900 text-red-700 dark:text-red-400 border border-red-300 dark:border-red-700 hover:border-red-400 dark:hover:border-red-500 transition-all font-mono text-xs uppercase"
                >
                  <Trash2 className="h-3 w-3 mr-1.5" />
                  Terminate
                </Button>
              </AlertDialogTrigger>
              <AlertDialogContent className="bg-card border border-destructive dark:border-red-700">
                <AlertDialogHeader>
                  <AlertDialogTitle className="text-destructive dark:text-red-400 font-mono uppercase">âš ï¸ Terminate Strategy</AlertDialogTitle>
                  <AlertDialogDescription className="text-muted-foreground font-mono text-sm">
                    {'>> '}CONFIRM DELETION OF &quot;{strategy.name}&quot;<br/>
                    {'>> '}THIS OPERATION IS IRREVERSIBLE<br/>
                    {'>> '}ALL ASSOCIATED DATA WILL BE PURGED
                  </AlertDialogDescription>
                </AlertDialogHeader>
                <AlertDialogFooter>
                  <AlertDialogCancel className="bg-muted border-border text-foreground hover:bg-muted/80 font-mono text-xs uppercase">Abort</AlertDialogCancel>
                  <AlertDialogAction
                    onClick={handleDeleteStrategy}
                    className="bg-destructive dark:bg-red-950 hover:bg-destructive/90 dark:hover:bg-red-900 text-destructive-foreground dark:text-red-400 border border-destructive dark:border-red-700 font-mono text-xs uppercase"
                  >
                    Confirm Termination
                  </AlertDialogAction>
                </AlertDialogFooter>
              </AlertDialogContent>
            </AlertDialog>
          </div>
        </div>

        {/* Compact Stats Bar - Terminal Style */}
        <div className="bg-card/50 border border-border">
          {/* Config Stats Row */}
          <div className="flex flex-wrap items-center divide-x divide-border border-b border-border">
            <div className="flex items-center gap-2 px-4 py-2">
              <span className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground">::</span>
              <span className="text-[10px] font-mono uppercase text-muted-foreground">INIT BAL</span>
              <span className="text-sm font-mono font-bold text-primary dark:text-cyan-400">${positionSettings.startingBalance.toLocaleString()}</span>
            </div>
            <div className="flex items-center gap-2 px-4 py-2">
              <span className="text-[10px] font-mono uppercase text-muted-foreground">POS SIZE</span>
              <span className="text-sm font-mono font-bold text-primary dark:text-cyan-400">${positionSettings.model.size.toLocaleString()}</span>
            </div>
            <div className="flex items-center gap-2 px-4 py-2">
              <span className="text-[10px] font-mono uppercase text-muted-foreground">MAX POS</span>
              <span className="text-sm font-mono font-bold text-primary dark:text-cyan-400">{positionSettings.maxConcurrentPositions}</span>
            </div>
            <div className="flex items-center gap-2 px-4 py-2">
              <span className="text-[10px] font-mono uppercase text-muted-foreground">TRADES</span>
              <span className="text-sm font-mono font-bold text-green-600 dark:text-green-400">{tradesResponse?.totalTrades ?? trades.length}</span>
            </div>
            <div className="flex items-center gap-2 px-4 py-2">
              <span className="text-[10px] font-mono uppercase text-muted-foreground">WIN RATE</span>
              <span className={`text-sm font-mono font-bold ${winRatePercent && winRatePercent >= 50 ? 'text-green-600 dark:text-green-400' : 'text-yellow-600 dark:text-yellow-400'}`}>
                {winRatePercent !== undefined ? `${winRatePercent.toFixed(1)}%` : '-'}
              </span>
            </div>
            <div className="flex items-center gap-2 px-4 py-2">
              <span className="text-[10px] font-mono uppercase text-muted-foreground">AVG P/L</span>
              <span className={`text-sm font-mono font-bold ${averageProfitValue !== undefined ? (averageProfitValue >= 0 ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400') : 'text-muted-foreground'}`}>
                {averageProfitValue !== undefined ? formatPrice(averageProfitValue) : '-'}
              </span>
            </div>
          </div>
          
          {/* Live Status Row - Inline with Balance History */}
          <div className="grid grid-cols-1 lg:grid-cols-12">
            {/* Balance History Chart - Integrated */}
            <div className="lg:col-span-8 border-r border-border">
              <BalanceHistoryChart 
                strategyId={strategyId!}
                startingBalance={positionSettings.startingBalance}
                compact={true}
              />
            </div>
            
            {/* Live Strategy State Panel - Compact */}
            <div className="lg:col-span-4">
              <StrategyStatePanel 
                strategyId={strategyId!} 
                startingBalance={positionSettings.startingBalance}
                compact={true}
              />
            </div>
          </div>
        </div>

        {/* Main Content */}
        <div className="w-full">

          <div className="space-y-3 mt-3">
            <div className="flex items-center justify-between flex-wrap gap-2 border-b border-border pb-2">
              <h2 className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground"># Trading Activity</h2>
              
              <ToggleGroup 
                type="single" 
                value={tradeType} 
                onValueChange={handleTradeTypeChange}
                className="bg-card p-0.5 border border-border"
              >
                <ToggleGroupItem value="all" aria-label="Show all trades" className="data-[state=on]:bg-primary/20 dark:data-[state=on]:bg-cyan-950 data-[state=on]:text-primary dark:data-[state=on]:text-cyan-400 data-[state=on]:border-primary dark:data-[state=on]:border-cyan-700 text-muted-foreground hover:text-foreground px-2 py-0.5 text-[10px] font-mono uppercase">
                  All
                </ToggleGroupItem>
                <ToggleGroupItem value="paper" aria-label="Show paper trades" className="data-[state=on]:bg-yellow-100 dark:data-[state=on]:bg-yellow-950 data-[state=on]:text-yellow-700 dark:data-[state=on]:text-yellow-400 data-[state=on]:border-yellow-300 dark:data-[state=on]:border-yellow-700 text-muted-foreground hover:text-foreground px-2 py-0.5 text-[10px] font-mono uppercase">
                  Paper
                </ToggleGroupItem>
                <ToggleGroupItem value="live" aria-label="Show live trades" className="data-[state=on]:bg-green-100 dark:data-[state=on]:bg-green-950 data-[state=on]:text-green-700 dark:data-[state=on]:text-green-400 data-[state=on]:border-green-300 dark:data-[state=on]:border-green-700 text-muted-foreground hover:text-foreground px-2 py-0.5 text-[10px] font-mono uppercase">
                  Live
                </ToggleGroupItem>
              </ToggleGroup>
            </div>

            {tradesError ? (
              <div className="bg-red-100 dark:bg-red-950/50 border border-red-300 dark:border-red-700 text-red-700 dark:text-red-400 p-4 font-mono text-sm">
                <span className="text-red-600 dark:text-red-500">ERROR:</span> Failed to load trades for this strategy
              </div>
            ) : (
              <>
                <TradeStatistics trades={filteredTrades} />
                
                {/* Main Layout: Chart | Calendar/Table */}
                <div className="grid grid-cols-1 lg:grid-cols-12 gap-3">
                  {/* Chart (Main Focus) */}
                  <div className="lg:col-span-8">
                    <Card className="p-3 bg-card/50 border border-border">
                      <div className="mb-2 border-b border-border pb-2 flex items-center justify-between flex-wrap gap-2">
                        <h3 className="text-[10px] font-mono uppercase tracking-wider text-primary dark:text-cyan-400"># Balance Over Time</h3>
                        <div className="flex items-center gap-3">
                          <div className="flex items-center gap-1.5">
                            <div className="w-3 h-0.5 bg-[#8b5cf6]"></div>
                            <span className="text-[9px] font-mono text-muted-foreground">Strategy</span>
                          </div>
                          <div className="flex items-center gap-1.5">
                            <div className="w-3 h-0.5 bg-[#f59e0b] border-dashed border-t-2"></div>
                            <span className="text-[9px] font-mono text-muted-foreground">SPY</span>
                          </div>
                        </div>
                      </div>
                      
                      <div ref={chartContainerRef} className="w-full h-[400px] bg-background dark:bg-[#0a0e17] border border-border" />
                    </Card>

                    {/* Selected Trades Section */}
                    {selectedDate && selectedDayTrades.length > 0 && (
                      <Card className="p-3 bg-card/50 border border-border mt-2">
                        <div className="mb-2 border-b border-border pb-2">
                          <div className="flex items-center justify-between">
                            <h3 className="text-[10px] font-mono uppercase tracking-wider text-primary dark:text-cyan-400">
                              # {selectedDate.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })} Trades
                            </h3>
                            <div className="flex items-center gap-2">
                              <span className={`text-sm font-mono font-bold ${selectedDayProfit >= 0 ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'}`}>
                                {formatPrice(selectedDayProfit)}
                              </span>
                              <button
                                onClick={() => setSelectedDate(undefined)}
                                className="text-[10px] font-mono text-muted-foreground hover:text-destructive dark:hover:text-red-400 transition-colors"
                              >
                                âœ•
                              </button>
                            </div>
                          </div>
                        </div>
                        
                        <div className="space-y-1.5 max-h-[200px] overflow-y-auto pr-1 custom-scrollbar">
                          {selectedDayTrades.map((trade) => (
                            <div
                              key={trade.id}
                              onClick={() => handleTradeClick(trade)}
                              className="p-2 border border-border bg-muted/30 hover:border-primary dark:hover:border-cyan-700 hover:bg-muted/50 dark:hover:bg-gray-900 transition-all cursor-pointer"
                            >
                              <div className="flex justify-between items-center">
                                <span className="font-mono font-bold text-xs text-foreground">{trade.ticker}</span>
                                <span
                                  className={`text-xs font-mono font-bold ${
                                    trade.profit >= 0 ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'
                                  }`}
                                >
                                  {formatPrice(trade.profit)}
                                </span>
                              </div>
                              <div className="flex justify-between text-[9px] font-mono text-muted-foreground mt-1">
                                <span>{formatPrice(trade.entryPrice)} â†’ {formatPrice(trade.closePrice)}</span>
                                <span>x{trade.shares}</span>
                              </div>
                            </div>
                          ))}
                        </div>
                      </Card>
                    )}
                  </div>
                  
                  {/* Right Sidebar - Calendar and Table */}
                  <div className="lg:col-span-4 space-y-3">
                    {/* Calendar */}
                    <Card className="p-3 bg-card/50 border border-border">
                      <div className="mb-2 border-b border-border pb-2">
                        <h3 className="text-[10px] font-mono uppercase tracking-wider text-primary dark:text-cyan-400"># Calendar</h3>
                      </div>
                      <div className="bg-muted/30 dark:bg-gray-950/50 border border-border p-2">
                        {/* Calendar Header */}
                        <div className="flex justify-between items-center mb-2 border-b border-border pb-2">
                          <button 
                            onClick={goToPreviousMonth}
                            className="p-0.5 hover:bg-muted border border-border hover:border-primary dark:hover:border-cyan-700 transition-colors"
                          >
                            <ChevronLeft className="h-3 w-3 text-muted-foreground hover:text-primary dark:hover:text-cyan-400" />
                          </button>
                          <h3 className="text-[10px] font-mono uppercase tracking-wider text-primary dark:text-cyan-400">{monthYearDisplay}</h3>
                          <button 
                            onClick={goToNextMonth}
                            className="p-0.5 hover:bg-muted border border-border hover:border-primary dark:hover:border-cyan-700 transition-colors"
                          >
                            <ChevronRight className="h-3 w-3 text-muted-foreground hover:text-primary dark:hover:text-cyan-400" />
                          </button>
                        </div>
                        
                        {/* Days of Week Header */}
                        <div className="grid grid-cols-7 gap-0.5 mb-1">
                          {daysOfWeek.map(day => (
                            <div key={day} className="text-center text-[8px] font-mono uppercase tracking-wider text-muted-foreground py-0.5">
                              {day.substring(0, 2)}
                            </div>
                          ))}
                        </div>
                        
                        {/* Calendar Grid */}
                        <div className="grid grid-cols-7 gap-0.5">
                          {calendarData.flat().map((day, index) => (
                            <button
                              key={index}
                              onClick={() => handleDayClick(day)}
                              disabled={day === null}
                              className={`
                                h-6 w-full flex items-center justify-center text-[10px] font-mono border transition-colors
                                ${day === null ? 'text-muted-foreground/30 border-transparent' : 'text-foreground border-border hover:border-primary dark:hover:border-cyan-700'} 
                                ${isSelected(day) ? 'border-2 border-primary dark:border-cyan-500 text-primary dark:text-cyan-400 font-bold bg-primary/10 dark:bg-cyan-950/30' : ''}
                                ${isToday(day) ? 'border-2 border-yellow-500 text-yellow-600 dark:text-yellow-400 font-bold' : ''}
                                ${getDayColor(day)}
                              `}
                            >
                              {day}
                            </button>
                          ))}
                        </div>
                      </div>
                      
                      {/* Selected Day Summary */}
                      {selectedDate && (
                        <div className="mt-2 p-2 bg-muted/30 dark:bg-gray-950/50 border border-border">
                          <div className="flex items-center justify-between mb-1">
                            <span className="text-[10px] font-mono uppercase text-muted-foreground">
                              {selectedDate.toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' })}
                            </span>
                            <span className="text-[10px] font-mono text-primary dark:text-cyan-400">
                              {selectedDayTrades.length} {selectedDayTrades.length === 1 ? 'TRADE' : 'TRADES'}
                            </span>
                          </div>
                          <div className="flex items-center justify-between">
                            <span className="text-[9px] font-mono uppercase text-muted-foreground">P/L:</span>
                            <span className={`text-sm font-mono font-bold ${selectedDayProfit >= 0 ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'}`}>
                              {formatPrice(selectedDayProfit)}
                            </span>
                          </div>
                        </div>
                      )}
                    </Card>
                    
                    {/* Table View */}
                    <Card className="p-3 bg-card/50 border border-border">
                      <div className="mb-2 border-b border-border pb-2">
                        <h3 className="text-[10px] font-mono uppercase tracking-wider text-primary dark:text-cyan-400"># Recent Trades</h3>
                      </div>
                      {trades.length > 0 ? (
                        <div className="space-y-2">
                          <div className="max-h-[280px] overflow-y-auto custom-scrollbar">
                            <TradesTable 
                              trades={getPaginatedData()} 
                              sortConfig={sortConfig}
                              onSort={sortData}
                              compact={true}
                            />
                          </div>
                          
                          {/* Compact Pagination Controls */}
                          <div className="flex items-center justify-between flex-wrap gap-2 pt-2 border-t border-border">
                            <div className="flex items-center gap-2">
                              <select
                                value={itemsPerPage}
                                onChange={(e) => {
                                  setItemsPerPage(Number(e.target.value));
                                  setCurrentPage(1);
                                }}
                                className="px-1.5 py-0.5 border border-border bg-card text-primary dark:text-cyan-400 text-[10px] font-mono hover:border-primary dark:hover:border-cyan-700 transition-colors"
                              >
                                <option value={10}>10</option>
                                <option value={25}>25</option>
                                <option value={50}>50</option>
                              </select>
                              <span className="text-[9px] font-mono text-muted-foreground">
                                {Math.min((currentPage - 1) * itemsPerPage + 1, filteredTrades.length)}-{Math.min(currentPage * itemsPerPage, filteredTrades.length)}/{filteredTrades.length}
                              </span>
                            </div>
                            
                            <div className="flex items-center gap-0.5">
                              <button
                                onClick={() => setCurrentPage(1)}
                                disabled={!hasPrevPage}
                                className="px-1.5 py-0.5 border border-border bg-card text-foreground hover:border-primary dark:hover:border-cyan-700 hover:text-primary dark:hover:text-cyan-400 disabled:opacity-30 font-mono text-[10px] transition-colors"
                              >
                                â—„â—„
                              </button>
                              <button
                                onClick={() => setCurrentPage(currentPage - 1)}
                                disabled={!hasPrevPage}
                                className="px-1.5 py-0.5 border border-border bg-card text-foreground hover:border-primary dark:hover:border-cyan-700 hover:text-primary dark:hover:text-cyan-400 disabled:opacity-30 font-mono text-[10px] transition-colors"
                              >
                                â—„
                              </button>
                              <span className="px-2 text-[10px] font-mono text-muted-foreground">{currentPage}/{totalPages}</span>
                              <button
                                onClick={() => setCurrentPage(currentPage + 1)}
                                disabled={!hasNextPage}
                                className="px-1.5 py-0.5 border border-border bg-card text-foreground hover:border-primary dark:hover:border-cyan-700 hover:text-primary dark:hover:text-cyan-400 disabled:opacity-30 font-mono text-[10px] transition-colors"
                              >
                                â–º
                              </button>
                              <button
                                onClick={() => setCurrentPage(totalPages)}
                                disabled={!hasNextPage}
                                className="px-1.5 py-0.5 border border-border bg-card text-foreground hover:border-primary dark:hover:border-cyan-700 hover:text-primary dark:hover:text-cyan-400 disabled:opacity-30 font-mono text-[10px] transition-colors"
                              >
                                â–ºâ–º
                              </button>
                            </div>
                          </div>
                        </div>
                      ) : (
                        <div className="p-4 border border-dashed border-border text-center">
                          <p className="text-muted-foreground font-mono text-xs">[ NO TRADES ]</p>
                          <p className="text-[10px] text-muted-foreground/60 mt-1 font-mono">{'>> '}Awaiting execution...</p>
                        </div>
                      )}
                    </Card>
                  </div>
                </div>
              </>
            )}
          </div>
        </div>
      </div>
      
    </div>
  );
};

export default StrategyDetailPage; 