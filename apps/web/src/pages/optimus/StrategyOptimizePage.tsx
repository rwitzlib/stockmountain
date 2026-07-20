import { getAuthHeaders } from '../../api/authToken';
import { useState, useRef, useEffect, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Strategy } from '../../types/strategy';
import { Trade } from '../../types/trade';
import { TradeStatistics } from '../../components/trades/TradeStatistics';
import { TradesTable } from '../../components/trades/TradesTable';
import { Button } from '../../components/ui/button';
import { ArrowLeft, Beaker, ChevronLeft, ChevronRight, TrendingUp, BarChart3 } from 'lucide-react';
import { strategyApi } from '../../api/strategyApi';
import { Badge } from '../../components/ui/badge';
import { Card } from '../../components/ui/card';
import { formatPrice } from '../../utils/chartUtils';
import { createChart, LineSeries, ColorType, LineStyle, type IChartApi, type ISeriesApi } from 'lightweight-charts';
import type { LogicalRange } from 'lightweight-charts';
import StrategyOptimizePanel from '../../components/optimization/StrategyOptimizePanel';

// Local type to reflect trade API response
type TradeResponse = {
  totalTrades: number;
  totalProfit: number;
  averageProfit: number;
  winRate: number;
  maxConcurrentTrades: number;
  trades: Trade[];
};

const StrategyOptimizePage = () => {
  const { strategyId } = useParams<{ strategyId: string }>();
  const navigate = useNavigate();
  
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
  const [optimizedTrades, setOptimizedTrades] = useState<Trade[] | null>(null);
  const chartContainerRef = useRef<HTMLDivElement>(null);
  const chartRef = useRef<IChartApi | null>(null);
  const lineSeriesRef = useRef<ISeriesApi<'Line'> | null>(null);
  const baselineSeriesRef = useRef<ISeriesApi<'Line'> | null>(null);
  const hasUserInteractedRef = useRef(false);
  const hasInitialFitRef = useRef(false);
  const tradeMapRef = useRef<Map<number, Date>>(new Map());
  const [isDarkMode, setIsDarkMode] = useState(() => 
    document.documentElement.classList.contains('dark')
  );
  
  // Pagination state
  const [currentPage, setCurrentPage] = useState(1);
  const [itemsPerPage, setItemsPerPage] = useState(25);

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

  const trades = tradesResponse?.trades || [];

  // Use new structure if available, fallback to legacy structure
  const positionSettings = strategy?.positionSettings || {
    startingBalance: 10000,
    allowSimultaneous: false,
    maxConcurrentPositions: 1,
    model: {
      type: 'Fixed' as const,
      size: 1000
    }
  };

  // Use optimized trades if available, otherwise use all trades
  const sourceTrades = optimizedTrades !== null ? optimizedTrades : trades;
  
  const filteredTrades = sourceTrades.filter((trade: Trade) => {
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
    let cells: (number | null)[] = [];
    
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
    const newSelectedDate = new Date(currentYear, currentMonth, day);
    setSelectedDate(newSelectedDate);
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

  const handleOptimizationResult = (newTrades: Trade[]) => {
    setOptimizedTrades(newTrades);
    setCurrentPage(1);
  };

  const handleClearOptimization = () => {
    setOptimizedTrades(null);
    setCurrentPage(1);
  };

  // Calculate cumulative balance data for chart
  const getCumulativeBalanceData = useCallback(() => {
    if (!strategy || filteredTrades.length === 0) {
      return { balanceData: [], tradeMap: new Map() };
    }

    const startingBalance = positionSettings.startingBalance;
    let cumulativeBalance = startingBalance;
    
    const sortedTrades = [...filteredTrades]
      .filter(trade => trade.closedAt && trade.profit !== undefined)
      .sort((a, b) => new Date(a.closedAt).getTime() - new Date(b.closedAt).getTime());

    if (sortedTrades.length === 0) {
      return { balanceData: [], tradeMap: new Map() };
    }

    const tradesByDay = new Map<string, Trade[]>();
    sortedTrades.forEach(trade => {
      const tradeDate = new Date(trade.closedAt);
      const dayKey = `${tradeDate.getFullYear()}-${tradeDate.getMonth()}-${tradeDate.getDate()}`;
      if (!tradesByDay.has(dayKey)) {
        tradesByDay.set(dayKey, []);
      }
      tradesByDay.get(dayKey)!.push(trade);
    });

    const balanceData: { time: number; value: number }[] = [];
    const tradeMap = new Map<number, Date>();

    const sortedDayKeys = Array.from(tradesByDay.keys()).sort((a, b) => {
      const [yearA, monthA, dayA] = a.split('-').map(Number);
      const [yearB, monthB, dayB] = b.split('-').map(Number);
      return new Date(yearA, monthA, dayA).getTime() - new Date(yearB, monthB, dayB).getTime();
    });

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
    }

    sortedDayKeys.forEach(dayKey => {
      const [year, month, day] = dayKey.split('-').map(Number);
      const dayDate = new Date(year, month, day);
      dayDate.setHours(16, 0, 0, 0);
      const dayTimestamp = Math.floor(dayDate.getTime() / 1000);

      const dayTrades = tradesByDay.get(dayKey)!;
      const dayProfit = dayTrades.reduce((sum, trade) => sum + trade.profit, 0);
      cumulativeBalance += dayProfit;

      balanceData.push({
        time: dayTimestamp,
        value: cumulativeBalance
      });
      tradeMap.set(dayTimestamp, dayDate);
    });

    return { balanceData, tradeMap };
  }, [strategy, filteredTrades, positionSettings]);

  // Theme helpers
  const applyTheme = useCallback(() => {
    if (!chartRef.current || !lineSeriesRef.current) return;
    const colors = isDarkMode ? {
      background: 'transparent',
      text: '#8b93a1',
      grid: 'rgba(148,163,184,0.08)',
      crosshair: '#6b7280',
      line: '#10b981',
      markerBorder: '#10b981',
      markerBg: '#1C2026',
    } : {
      background: '#ffffff',
      text: '#1f2937',
      grid: '#e5e7eb',
      crosshair: '#9ca3af',
      line: '#10b981',
      markerBorder: '#10b981',
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
    
    if (baselineSeriesRef.current) {
      baselineSeriesRef.current.applyOptions({
        color: isDarkMode ? '#6b7280' : '#9ca3af',
      });
    }
  }, [isDarkMode]);

  // Create chart
  useEffect(() => {
    if (!chartContainerRef.current || !strategy) return;
    if (chartRef.current) return;
    
    const initialIsDark = document.documentElement.classList.contains('dark');
    const initialColors = initialIsDark ? {
      background: 'transparent',
      text: '#8b93a1',
      grid: 'rgba(148,163,184,0.08)',
      crosshair: '#6b7280',
      border: 'rgba(148,163,184,0.15)',
    } : {
      background: '#ffffff',
      text: '#1f2937',
      grid: '#e5e7eb',
      crosshair: '#9ca3af',
      border: '#d1d5db',
    };
    
    const chart = createChart(chartContainerRef.current, {
      width: chartContainerRef.current.clientWidth,
      height: 300,
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
      color: '#10b981',
      lineWidth: 2,
      crosshairMarkerVisible: true,
      crosshairMarkerRadius: 5,
      crosshairMarkerBorderColor: '#10b981',
      crosshairMarkerBackgroundColor: initialIsDark ? '#1C2026' : '#ffffff',
      title: 'Filtered Balance',
    });
    lineSeriesRef.current = line;

    const baseline = chart.addSeries(LineSeries, {
      color: initialIsDark ? '#6b7280' : '#9ca3af',
      lineWidth: 1,
      lineStyle: LineStyle.Dotted,
      crosshairMarkerVisible: false,
    });
    baselineSeriesRef.current = baseline;

    const ts = chart.timeScale();
    ts.subscribeVisibleTimeRangeChange(() => {
      hasUserInteractedRef.current = true;
    });

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

    const handleResize = () => {
      if (!chartContainerRef.current || !chartRef.current) return;
      const ts = chartRef.current.timeScale();
      const logical = ts.getVisibleLogicalRange();
      chartRef.current.applyOptions({
        width: chartContainerRef.current.clientWidth,
        height: 300,
      });
      if (logical) ts.setVisibleLogicalRange(logical as LogicalRange);
    };
    window.addEventListener('resize', handleResize);

    applyTheme();

    return () => {
      window.removeEventListener('resize', handleResize);
      if (chartContainerRef.current) chartContainerRef.current.removeEventListener('click', handleChartClick);
      if (chartRef.current) { chartRef.current.remove(); chartRef.current = null; }
      lineSeriesRef.current = null;
      baselineSeriesRef.current = null;
      hasInitialFitRef.current = false;
      hasUserInteractedRef.current = false;
    };
  }, [strategy, applyTheme]);

  // Update chart data
  useEffect(() => {
    if (!chartRef.current || !lineSeriesRef.current || !baselineSeriesRef.current || !strategy) return;
    
    const { balanceData, tradeMap } = getCumulativeBalanceData();
    
    if (balanceData.length === 0) {
      lineSeriesRef.current.setData([]);
      baselineSeriesRef.current.setData([]);
      return;
    }

    const startingBalance = positionSettings.startingBalance;
    const ts = chartRef.current.timeScale();
    const savedLogical = hasUserInteractedRef.current ? (ts.getVisibleLogicalRange() as LogicalRange | null) : null;

    try {
      lineSeriesRef.current.setData(balanceData);
      tradeMapRef.current = tradeMap;

      const baselineData = [
        { time: balanceData[0].time, value: startingBalance },
        { time: balanceData[balanceData.length - 1].time, value: startingBalance }
      ];
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
  }, [getCumulativeBalanceData, strategy, positionSettings]);

  // Apply theme when it changes
  useEffect(() => {
    applyTheme();
  }, [isDarkMode, applyTheme]);

  if (isLoadingStrategy) {
    return (
      <div className="min-h-screen bg-background p-4 md:p-8">
        <div className="max-w-7xl mx-auto">
          <div className="text-center py-8 text-muted-foreground text-sm">
            <div className="animate-pulse">Loading strategy data…</div>
          </div>
        </div>
      </div>
    );
  }

  if (!strategy) {
    return (
      <div className="min-h-screen bg-background p-4 md:p-8">
        <div className="max-w-7xl mx-auto">
          <div className="text-center py-8 text-destructive dark:text-red-400 text-sm">
            <div className="font-medium">Strategy not found</div>
            <div className="text-muted-foreground text-xs mt-2">Invalid strategy ID or access denied</div>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-background p-3 md:p-6 pt-20 md:pt-6">
      <div className="max-w-7xl mx-auto space-y-4">
        {/* Header */}
        <div className="flex items-center justify-between border-b border-border pb-4">
          <div className="flex items-center gap-4">
            <Button
              variant="outline"
              size="sm"
              className="text-muted-foreground hover:bg-accent hover:text-foreground transition-colors"
              onClick={() => navigate(`/optimus/strategy/${strategyId}`)}
            >
              <ArrowLeft className="h-4 w-4 mr-1" />
              <span className="text-xs">Back to Strategy</span>
            </Button>
            <div>
              <div className="flex items-center gap-2">
                <Beaker className="w-5 h-5 text-muted-foreground" />
                <h1 className="text-xl font-semibold tracking-tight text-foreground">
                  Trade Optimization
                </h1>
              </div>
              <p className="text-sm text-muted-foreground mt-1">
                Analyzing trades for <span className="text-foreground">{strategy.name}</span>
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <Badge 
              variant="default"
              className={`rounded-full px-2.5 py-0.5 text-[11px] font-semibold border-transparent ${
                strategy.type === 'Paper' 
                  ? 'bg-yellow-500/10 text-yellow-600 dark:text-yellow-400' 
                  : 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400'
              }`}
            >
              {strategy.type || 'Paper'}
            </Badge>
            <Badge variant="outline" className="rounded-full border-border px-2.5 py-0.5 text-[11px] font-medium text-muted-foreground">
              {trades.length} Total Trades
            </Badge>
          </div>
        </div>

        {/* Main Content */}
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-4">
          {/* Left: Optimization Panel */}
          <div className="lg:col-span-5">
            <StrategyOptimizePanel 
              strategyId={strategyId!} 
              onOptimizationResult={handleOptimizationResult}
            />
          </div>

          {/* Right: Results */}
          <div className="lg:col-span-7 space-y-4">
            {/* Optimization Status */}
            {optimizedTrades !== null && (
              <Card className={`p-4 border ${
                optimizedTrades.length > 0 
                  ? 'bg-emerald-50 dark:bg-emerald-950/30 border-emerald-300 dark:border-emerald-700' 
                  : 'bg-yellow-50 dark:bg-yellow-950/30 border-yellow-300 dark:border-yellow-700'
              }`}>
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <div className={`w-3 h-3 rounded-full ${
                      optimizedTrades.length > 0 ? 'bg-emerald-500' : 'bg-yellow-500'
                    }`} />
                    <div>
                      <div className={`text-sm font-medium ${
                        optimizedTrades.length > 0 ? 'text-emerald-700 dark:text-emerald-400' : 'text-yellow-700 dark:text-yellow-400'
                      }`}>
                        {optimizedTrades.length > 0 
                          ? `${optimizedTrades.length} trades match your filters`
                          : 'No trades match the filters'
                        }
                      </div>
                      <div className="text-xs text-muted-foreground">
                        Out of {trades.length} total trades
                      </div>
                    </div>
                  </div>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={handleClearOptimization}
                    className="text-xs"
                  >
                    Clear Filter
                  </Button>
                </div>
              </Card>
            )}

            {/* Statistics */}
            {filteredTrades.length > 0 && (
              <TradeStatistics trades={filteredTrades} />
            )}

            {/* Balance Chart */}
            <Card className="p-4">
              <div className="flex items-center justify-between mb-3 pb-3 border-b border-border">
                <div className="flex items-center gap-2">
                  <BarChart3 className="w-4 h-4 text-muted-foreground" />
                  <h3 className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">
                    Filtered Balance
                  </h3>
                </div>
                <div className="text-xs text-muted-foreground tabular-nums">
                  Starting: {formatPrice(positionSettings.startingBalance)}
                </div>
              </div>
              <div ref={chartContainerRef} className="w-full h-[300px] rounded-lg" />
            </Card>

            {/* Calendar */}
            <Card className="p-4">
              <div className="mb-4 border-b border-border pb-3">
                <h3 className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">Profit Calendar</h3>
              </div>
              
              <div className="rounded-lg border border-border/80 bg-muted/30 p-4">
                <div className="flex justify-between items-center mb-4 border-b border-border pb-3">
                  <button 
                    onClick={goToPreviousMonth}
                    className="p-1 rounded-md hover:bg-accent transition-colors"
                  >
                    <ChevronLeft className="h-4 w-4 text-muted-foreground" />
                  </button>
                  <h3 className="text-sm font-medium text-foreground">{monthYearDisplay}</h3>
                  <button 
                    onClick={goToNextMonth}
                    className="p-1 rounded-md hover:bg-accent transition-colors"
                  >
                    <ChevronRight className="h-4 w-4 text-muted-foreground" />
                  </button>
                </div>
                
                <div className="grid grid-cols-7 gap-1 mb-2">
                  {daysOfWeek.map(day => (
                    <div key={day} className="text-center text-[10px] font-medium uppercase tracking-wider text-muted-foreground py-1">
                      {day}
                    </div>
                  ))}
                </div>
                
                <div className="grid grid-cols-7 gap-1">
                  {calendarData.flat().map((day, index) => (
                    <button
                      key={index}
                      onClick={() => handleDayClick(day)}
                      disabled={day === null}
                      className={`
                        h-8 w-full flex items-center justify-center text-xs tabular-nums rounded-md border transition-colors
                        ${day === null ? 'text-muted-foreground/30 border-transparent' : 'text-foreground border-border hover:bg-accent'} 
                        ${isSelected(day) ? 'border-primary bg-accent text-foreground font-semibold' : ''}
                        ${isToday(day) ? 'border-yellow-500 text-yellow-600 dark:text-yellow-400 font-semibold' : ''}
                        ${getDayColor(day)}
                      `}
                    >
                      {day}
                    </button>
                  ))}
                </div>
              </div>
              
              {selectedDate && selectedDayTrades.length > 0 && (
                <div className="mt-4 p-3 rounded-lg border border-border/80 bg-muted/30">
                  <div className="flex items-center justify-between mb-2">
                    <span className="text-xs text-muted-foreground">
                      {selectedDate.toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' })}
                    </span>
                    <span className={`text-sm font-bold tabular-nums ${selectedDayProfit >= 0 ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'}`}>
                      {formatPrice(selectedDayProfit)}
                    </span>
                  </div>
                  <div className="text-xs text-muted-foreground">
                    {selectedDayTrades.length} trade{selectedDayTrades.length !== 1 ? 's' : ''}
                  </div>
                </div>
              )}
            </Card>

            {/* Trades Table */}
            <Card className="p-4">
              <div className="mb-4 border-b border-border pb-3 flex items-center justify-between">
                <h3 className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">Filtered Trades</h3>
                <span className="text-xs text-muted-foreground tabular-nums">
                  {filteredTrades.length} trades
                </span>
              </div>
              
              {filteredTrades.length > 0 ? (
                <div className="space-y-4">
                  <TradesTable 
                    trades={getPaginatedData()} 
                    sortConfig={sortConfig}
                    onSort={sortData}
                  />
                  
                  {/* Pagination */}
                  <div className="flex items-center justify-between flex-wrap gap-4 p-3 rounded-lg border border-border/80">
                    <div className="flex items-center gap-4">
                      <div className="flex items-center gap-2">
                        <span className="text-xs text-muted-foreground">Show:</span>
                        <select
                          value={itemsPerPage}
                          onChange={(e) => {
                            setItemsPerPage(Number(e.target.value));
                            setCurrentPage(1);
                          }}
                          className="px-2 py-1 rounded-lg border border-input bg-card text-foreground text-xs"
                        >
                          <option value={10}>10</option>
                          <option value={25}>25</option>
                          <option value={50}>50</option>
                        </select>
                      </div>
                      
                      <div className="text-xs text-muted-foreground tabular-nums">
                        {Math.min((currentPage - 1) * itemsPerPage + 1, filteredTrades.length)}-{Math.min(currentPage * itemsPerPage, filteredTrades.length)} / {filteredTrades.length}
                      </div>
                    </div>
                    
                    <div className="flex items-center gap-1">
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setCurrentPage(1)}
                        disabled={!hasPrevPage}
                        className="text-xs"
                      >
                        «
                      </Button>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setCurrentPage(currentPage - 1)}
                        disabled={!hasPrevPage}
                        className="text-xs"
                      >
                        ‹
                      </Button>
                      <span className="px-3 text-xs tabular-nums">
                        {currentPage} / {totalPages || 1}
                      </span>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setCurrentPage(currentPage + 1)}
                        disabled={!hasNextPage}
                        className="text-xs"
                      >
                        ›
                      </Button>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setCurrentPage(totalPages)}
                        disabled={!hasNextPage}
                        className="text-xs"
                      >
                        »
                      </Button>
                    </div>
                  </div>
                </div>
              ) : (
                <div className="text-center py-8">
                  <p className="text-muted-foreground text-sm font-medium">No trades match the filters</p>
                  <p className="text-xs text-muted-foreground/60 mt-2">
                    {optimizedTrades !== null ? 'Try adjusting your filter criteria' : 'Run an optimization to filter trades'}
                  </p>
                </div>
              )}
            </Card>
          </div>
        </div>
      </div>
    </div>
  );
};

export default StrategyOptimizePage;

