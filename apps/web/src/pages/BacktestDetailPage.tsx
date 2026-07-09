import { useState, useEffect, useMemo, useCallback, useRef } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { BacktestStrategyCard } from '../components/BacktestStrategyCard';
import { FilterDisplay } from '../components/backtest/FilterDisplay';
import { backtestApi } from '../api/backtestApi';
import {
  TradingData,
  TradeStrategy,
  StrategyPortfolio,
  ExecutedTrade,
  EquityPoint,
} from '../types/types';
import { BacktestEntry, BacktestRequest } from '../types/backtest';
import { Strategy } from '../types/strategy';
import { Button } from '../components/ui/button';
import { Card } from '../components/ui/card';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '../components/ui/dropdown-menu';
import { ArrowLeft, Clock, RefreshCw, MoreVertical, Copy, Bot } from 'lucide-react';
import { formatDateNoTimezone } from '../utils/dateFormatter';
import { formatPrice } from '../utils/chartUtils';
import { toast } from '../hooks/use-toast';
import { createChart, LineSeries, ColorType, LineStyle, type IChartApi, type ISeriesApi } from 'lightweight-charts';
import { fetchMarketData } from '../services/polygon';
import { useQuery } from '@tanstack/react-query';

interface BacktestDetailData {
  tradingData: TradingData | null;
  backtestEntry: BacktestEntry;
  isProcessing: boolean;
}

interface TakenTradeRow extends ExecutedTrade {
  strategy: 'hold' | 'high' | 'other';
}

function normalizeStats(raw: unknown): TradeStrategy {
  const source = (raw && typeof raw === 'object' ? raw : {}) as Record<string, unknown>;
  const nested = source.stats && typeof source.stats === 'object'
    ? (source.stats as Record<string, unknown>)
    : source;

  const balanceChange = Number(nested.balanceChange ?? nested.sumProfit ?? 0);
  const sumProfit = Number(nested.sumProfit ?? nested.balanceChange ?? balanceChange);

  return {
    endBalance: Number(nested.endBalance ?? 0),
    balanceChange,
    sumProfit,
    winRatio: Number(nested.winRatio ?? 0),
    avgWin: Number(nested.avgWin ?? 0),
    avgLoss: Number(nested.avgLoss ?? 0),
    maxConcurrentPositions: Number(nested.maxConcurrentPositions ?? 0),
    totalTradesTaken: nested.totalTradesTaken != null ? Number(nested.totalTradesTaken) : undefined,
    averageDailyReturn: nested.averageDailyReturn != null ? Number(nested.averageDailyReturn) : undefined,
    dailyReturnStdDev: nested.dailyReturnStdDev != null ? Number(nested.dailyReturnStdDev) : undefined,
    sharpeRatio: nested.sharpeRatio != null ? Number(nested.sharpeRatio) : undefined,
    maxDrawdown: nested.maxDrawdown != null ? Number(nested.maxDrawdown) : undefined,
    profitFactor: nested.profitFactor != null ? Number(nested.profitFactor) : undefined,
  };
}

function normalizeEquity(raw: unknown): EquityPoint[] {
  if (!Array.isArray(raw)) return [];
  return raw.map((point) => {
    const p = (point && typeof point === 'object' ? point : {}) as Record<string, unknown>;
    return {
      date: String(p.date ?? ''),
      startCash: Number(p.startCash ?? 0),
      endCash: Number(p.endCash ?? 0),
      totalBalance: Number(p.totalBalance ?? 0),
      openPositions: Number(p.openPositions ?? 0),
      maxConcurrentPositions: Number(p.maxConcurrentPositions ?? 0),
      dayProfit: Number(p.dayProfit ?? 0),
      tradesTaken: Number(p.tradesTaken ?? 0),
    };
  });
}

function normalizeTrades(raw: unknown): ExecutedTrade[] {
  if (!Array.isArray(raw)) return [];
  return raw.map((trade) => {
    const t = (trade && typeof trade === 'object' ? trade : {}) as Record<string, unknown>;
    return {
      ticker: String(t.ticker ?? ''),
      boughtAt: String(t.boughtAt ?? ''),
      soldAt: String(t.soldAt ?? ''),
      startPrice: Number(t.startPrice ?? 0),
      endPrice: Number(t.endPrice ?? 0),
      shares: Number(t.shares ?? 0),
      startPosition: Number(t.startPosition ?? 0),
      endPosition: Number(t.endPosition ?? 0),
      profit: Number(t.profit ?? 0),
      stoppedOut: Boolean(t.stoppedOut),
    };
  });
}

function normalizePortfolio(raw: unknown): StrategyPortfolio {
  const source = (raw && typeof raw === 'object' ? raw : {}) as Record<string, unknown>;

  return {
    stats: normalizeStats(source),
    equity: normalizeEquity(source.equity),
    trades: normalizeTrades(source.trades),
  };
}

function normalizeTradingData(raw: unknown): TradingData | null {
  if (!raw || typeof raw !== 'object') return null;
  if (Array.isArray(raw)) return null;

  const source = raw as Record<string, unknown>;
  if (!source.hold && !source.high) return null;

  const otherRaw = source.other;
  const other =
    otherRaw == null
      ? null
      : normalizePortfolio(otherRaw);

  return {
    id: String(source.id ?? ''),
    creditsUsed: Number(source.creditsUsed ?? 0),
    hold: normalizePortfolio(source.hold),
    high: normalizePortfolio(source.high),
    other,
  };
}

function equityToChartData(equity: EquityPoint[]): { time: number; value: number }[] {
  return equity
    .filter((point) => point.date)
    .map((point) => {
      const dateStr = point.date.includes('T') ? point.date.split('T')[0] : point.date.split(' ')[0];
      const date = new Date(`${dateStr}T16:00:00`);
      return {
        time: Math.floor(date.getTime() / 1000),
        value: point.totalBalance,
      };
    })
    .sort((a, b) => a.time - b.time);
}

export function BacktestDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [data, setData] = useState<BacktestDetailData | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [isPolling, setIsPolling] = useState(false);
  const [tickerSearch, setTickerSearch] = useState('');
  const [debouncedTickerSearch, setDebouncedTickerSearch] = useState('');

  const chartContainerRef = useRef<HTMLDivElement>(null);
  const chartRef = useRef<IChartApi | null>(null);
  const holdSeriesRef = useRef<ISeriesApi<'Line'> | null>(null);
  const highSeriesRef = useRef<ISeriesApi<'Line'> | null>(null);
  const spySeriesRef = useRef<ISeriesApi<'Line'> | null>(null);
  const [isDarkMode, setIsDarkMode] = useState(() =>
    document.documentElement.classList.contains('dark')
  );

  useEffect(() => {
    if (id) {
      fetchBacktestDetails();
    }
  }, [id]);

  useEffect(() => {
    let interval: NodeJS.Timeout | null = null;

    const status = data?.backtestEntry?.status;
    const shouldPoll = status === 'Pending' || status === 'InProgress';

    if (shouldPoll) {
      setIsPolling(true);
      interval = setInterval(async () => {
        if (id) {
          try {
            const entry = await backtestApi.getBacktestEntry(id);
            await fetchBacktestResults(entry);
          } catch (e) {
            console.error('Failed to poll backtest status:', e);
          }
        }
      }, 15000);
    } else {
      setIsPolling(false);
    }

    return () => {
      if (interval) {
        clearInterval(interval);
      }
    };
  }, [data?.backtestEntry?.status, id]);

  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedTickerSearch(tickerSearch.trim());
    }, 300);
    return () => clearTimeout(timer);
  }, [tickerSearch]);

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

      if (isPending || isInProgress) {
        setData({
          tradingData: null,
          backtestEntry,
          isProcessing: true,
        });
        return;
      }

      if (isCompleted || isFailed) {
        try {
          const result = await backtestApi.getBacktestResult(backtestEntry.id);

          const isProcessingResponse =
            Array.isArray(result) &&
            result.length === 1 &&
            typeof result[0] === 'string' &&
            result[0].toLowerCase().includes('not completed');

          if (isProcessingResponse && !isFailed) {
            setData({
              tradingData: null,
              backtestEntry,
              isProcessing: true,
            });
          } else {
            setData({
              tradingData: isFailed ? null : normalizeTradingData(result),
              backtestEntry,
              isProcessing: false,
            });
          }
        } catch {
          setData({
            tradingData: null,
            backtestEntry,
            isProcessing: false,
          });
        }
      }
    } catch (e) {
      console.error('Failed to fetch backtest results:', e);
      if (entry) {
        const status = entry.status;
        setData({
          tradingData: null,
          backtestEntry: entry,
          isProcessing: status === 'Pending' || status === 'InProgress',
        });
      }
    }
  };

  const handleRefreshResults = () => {
    if (data?.backtestEntry) {
      fetchBacktestResults();
    }
  };

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
        return await fetchMarketData({
          ticker: 'SPY',
          multiplier: 1,
          timespan: 'day',
          from: fromDate,
          to: toDate,
        });
      } catch (err) {
        console.error('Error fetching SPY data:', err);
        return null;
      }
    },
    enabled: !!data?.backtestEntry?.start && !!data?.backtestEntry?.end && !!data?.tradingData,
  });

  const getRequestData = useCallback((backtestEntry: BacktestEntry) => {
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
        argument: req.entrySettings?.filters
          ? {
              operator: 'AND' as const,
              filters: req.entrySettings.filters.map((f: string) => ({ expression: f })),
            }
          : undefined,
        filters: req.entrySettings?.filters || [],
      };
    }

    if (backtestEntry.requestDetails) {
      return {
        positionInfo: backtestEntry.requestDetails.positionInfo || {
          startingBalance: 10000,
          maxConcurrentPositions: 1,
          positionSize: 1000,
        },
        exitInfo: backtestEntry.requestDetails.exitInfo,
        argument: backtestEntry.requestDetails.argument,
        filters: [] as string[],
      };
    }

    return {
      positionInfo: {
        startingBalance: 10000,
        maxConcurrentPositions: 1,
        positionSize: 1000,
      },
      exitInfo: {} as Record<string, unknown>,
      argument: undefined,
      filters: [] as string[],
    };
  }, []);

  const formatStopConfig = (config: any) => {
    if (!config) return 'Not set';

    const priceActionDisplay = config.priceActionType ? `${config.priceActionType} ` : '';

    if (config.type === 'percent') {
      return `${priceActionDisplay}${config.value}%`;
    }
    if (config.type === 'flat' || config.type === 'value') {
      return `${priceActionDisplay}$${config.value}`;
    }
    return `${priceActionDisplay}${config.value} (${config.type || 'unknown type'})`;
  };

  const formatTimeframe = (timeframe: any) => {
    if (!timeframe) return 'Not set';
    return `${timeframe.multiplier} ${timeframe.timespan}${timeframe.multiplier > 1 ? 's' : ''}`;
  };

  const formatDuration = (seconds: number | undefined) => {
    if (!seconds) return 'N/A';
    if (seconds < 60) return `${seconds}s`;
    if (seconds < 3600) return `${Math.floor(seconds / 60)}m ${seconds % 60}s`;
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const secs = seconds % 60;
    return `${hours}h ${minutes}m ${secs}s`;
  };

  const mapBacktestToStrategy = (backtestEntry: BacktestEntry): Strategy => {
    const requestData = getRequestData(backtestEntry);

    return {
      id: '',
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
        filters: [],
      },
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

    navigate('/optimus/dashboard', {
      state: {
        createStrategy: true,
        initialStrategyData: strategyData,
      },
    });
  };

  const takenTrades = useMemo((): TakenTradeRow[] => {
    if (!data?.tradingData) return [];

    const rows: TakenTradeRow[] = [
      ...data.tradingData.hold.trades.map((trade) => ({ ...trade, strategy: 'hold' as const })),
      ...data.tradingData.high.trades.map((trade) => ({ ...trade, strategy: 'high' as const })),
    ];

    if (data.tradingData.other) {
      rows.push(
        ...data.tradingData.other.trades.map((trade) => ({ ...trade, strategy: 'other' as const }))
      );
    }

    return rows.sort(
      (a, b) => new Date(b.boughtAt).getTime() - new Date(a.boughtAt).getTime()
    );
  }, [data?.tradingData]);

  const filteredTakenTrades = useMemo(() => {
    if (!debouncedTickerSearch) return takenTrades;
    const searchLower = debouncedTickerSearch.toLowerCase();
    return takenTrades.filter((trade) => trade.ticker.toLowerCase().includes(searchLower));
  }, [takenTrades, debouncedTickerSearch]);

  const getChartData = useCallback(() => {
    if (!data?.tradingData) {
      return { holdData: [] as { time: number; value: number }[], highData: [] as { time: number; value: number }[] };
    }

    return {
      holdData: equityToChartData(data.tradingData.hold.equity),
      highData: equityToChartData(data.tradingData.high.equity),
    };
  }, [data?.tradingData]);

  const getSpyPerformanceData = useCallback(() => {
    if (!spyDataResponse || !spyDataResponse.results || spyDataResponse.results.length === 0 || !data?.backtestEntry) {
      return [];
    }

    const startingBalance = getRequestData(data.backtestEntry).positionInfo.startingBalance || 10000;
    const sortedSpyBars = [...spyDataResponse.results].sort((a, b) => a.t - b.t);

    if (sortedSpyBars.length === 0) {
      return [];
    }

    const firstBar = sortedSpyBars[0];
    const initialPrice = firstBar.c;
    const initialShares = startingBalance / initialPrice;

    const startDate = new Date(data.backtestEntry.start);
    startDate.setHours(0, 0, 0, 0);
    const startTimestamp = Math.floor(startDate.getTime() / 1000);

    const spyDataPoints: { time: number; value: number }[] = [];

    spyDataPoints.push({
      time: startTimestamp,
      value: startingBalance,
    });

    sortedSpyBars.forEach((bar) => {
      const portfolioValue = initialShares * bar.c;
      const barTimestamp = Math.floor(bar.t / 1000);

      if (barTimestamp >= startTimestamp) {
        spyDataPoints.push({
          time: barTimestamp,
          value: portfolioValue,
        });
      }
    });

    return spyDataPoints;
  }, [spyDataResponse, data?.backtestEntry, getRequestData]);

  const applyTheme = () => {
    if (!chartRef.current) return;
    const colors = isDarkMode
      ? {
          background: '#0b1220',
          text: '#e5e7eb',
          grid: '#1f2937',
          crosshair: '#6b7280',
        }
      : {
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

  useEffect(() => {
    if (!chartContainerRef.current || !data?.tradingData) return;
    if (chartRef.current) return;

    const initialIsDark = document.documentElement.classList.contains('dark');
    const initialColors = initialIsDark
      ? {
          background: '#0b1220',
          text: '#e5e7eb',
          grid: '#1f2937',
          crosshair: '#6b7280',
          border: '#374151',
        }
      : {
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
      crosshair: {
        mode: 0,
        vertLine: { color: initialColors.crosshair },
        horzLine: { color: initialColors.crosshair },
      },
      grid: {
        vertLines: { color: initialColors.grid },
        horzLines: { color: initialColors.grid },
      },
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

    const handleResize = () => {
      if (!chartContainerRef.current || !chartRef.current) return;
      chartRef.current.applyOptions({
        width: chartContainerRef.current.clientWidth,
        height: chartContainerRef.current.clientHeight,
      });
    };
    window.addEventListener('resize', handleResize);

    applyTheme();

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

  useEffect(() => {
    if (!chartRef.current || !holdSeriesRef.current || !highSeriesRef.current) return;

    const { holdData, highData } = getChartData();
    const spyDataPoints = getSpyPerformanceData();

    holdSeriesRef.current.setData(holdData.length > 0 ? holdData : []);
    highSeriesRef.current.setData(highData.length > 0 ? highData : []);

    if (spySeriesRef.current) {
      spySeriesRef.current.setData(spyDataPoints.length > 0 ? spyDataPoints : []);
    }

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
  const showOther = tradingData?.other != null;

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
          {isPolling && !isProcessing && (
            <div className="text-[10px] font-mono uppercase tracking-wide text-muted-foreground">Polling…</div>
          )}

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

        {isProcessing && (
          <Card className="bg-yellow-500/10 border border-yellow-500/30 p-4">
            <div className="flex flex-wrap items-center gap-4 justify-between">
              <div className="flex items-start gap-3">
                <div className="animate-spin mt-1">
                  <RefreshCw className="w-5 h-5 text-yellow-600 dark:text-yellow-400" />
                </div>
                <div className="space-y-1">
                  <h3 className="text-sm font-mono uppercase tracking-wide text-yellow-600 dark:text-yellow-400">
                    Backtest Processing
                  </h3>
                  <p className="text-sm text-yellow-700 dark:text-yellow-300">
                    Your backtest is running. This typically takes 2-5 minutes. Results will appear automatically when
                    complete.
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

        <div className="grid grid-cols-1 lg:grid-cols-[1fr_320px] gap-6">
          <div className="space-y-6">
            {tradingData ? (
              <div className="space-y-6">
                <div className={`grid grid-cols-1 md:grid-cols-2 ${showOther ? 'lg:grid-cols-3' : 'lg:grid-cols-2'} gap-6`}>
                  <BacktestStrategyCard title="Hold Strategy" strategy={tradingData.hold.stats} />
                  <BacktestStrategyCard title="High Strategy" strategy={tradingData.high.stats} />
                  {showOther && tradingData.other && (
                    <BacktestStrategyCard title="Other Strategy" strategy={tradingData.other.stats} />
                  )}
                </div>

                <Card className="bg-card border border-border p-4 md:p-6">
                  <div className="mb-4 border-b border-border pb-3">
                    <div className="flex items-center justify-between mb-2">
                      <h3 className="text-xs font-mono uppercase tracking-wider text-primary dark:text-cyan-400">
                        # Balance Over Time
                      </h3>
                    </div>
                    <div className="flex items-center gap-4 flex-wrap">
                      {backtestEntry && (
                        <p className="text-[10px] font-mono text-muted-foreground">
                          {'>> '}INIT BAL:{' '}
                          {formatPrice(getRequestData(backtestEntry).positionInfo.startingBalance || 10000)}
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

                <Card className="bg-card border border-border p-4 md:p-6">
                  <div className="mb-4 border-b border-border pb-3 flex flex-wrap items-end justify-between gap-3">
                    <div>
                      <h3 className="text-xs font-mono uppercase tracking-wider text-primary dark:text-cyan-400">
                        # Taken Trades
                      </h3>
                      <p className="text-[10px] font-mono text-muted-foreground mt-1">
                        {filteredTakenTrades.length} of {takenTrades.length} trades
                      </p>
                    </div>
                    <div className="w-full sm:w-56">
                      <label className="block text-[9px] font-mono uppercase text-muted-foreground mb-1">
                        Search Ticker
                      </label>
                      <input
                        type="text"
                        value={tickerSearch}
                        onChange={(e) => setTickerSearch(e.target.value)}
                        placeholder="e.g. AAPL"
                        className="w-full px-2 py-1.5 bg-card border border-border text-foreground placeholder:text-muted-foreground text-xs font-mono rounded-md focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary h-8"
                      />
                    </div>
                  </div>

                  <div className="overflow-x-auto">
                    <table className="w-full text-left">
                      <thead>
                        <tr className="border-b border-border">
                          <th className="py-2 pr-3 text-[9px] font-mono uppercase tracking-wider text-muted-foreground">
                            Strategy
                          </th>
                          <th className="py-2 pr-3 text-[9px] font-mono uppercase tracking-wider text-muted-foreground">
                            Ticker
                          </th>
                          <th className="py-2 pr-3 text-[9px] font-mono uppercase tracking-wider text-muted-foreground">
                            Bought
                          </th>
                          <th className="py-2 pr-3 text-[9px] font-mono uppercase tracking-wider text-muted-foreground">
                            Sold
                          </th>
                          <th className="py-2 pr-3 text-[9px] font-mono uppercase tracking-wider text-muted-foreground text-right">
                            Shares
                          </th>
                          <th className="py-2 pr-3 text-[9px] font-mono uppercase tracking-wider text-muted-foreground text-right">
                            Profit
                          </th>
                          <th className="py-2 text-[9px] font-mono uppercase tracking-wider text-muted-foreground">
                            Stopped Out
                          </th>
                        </tr>
                      </thead>
                      <tbody>
                        {filteredTakenTrades.length === 0 ? (
                          <tr>
                            <td colSpan={7} className="py-8 text-center text-xs font-mono text-muted-foreground">
                              No taken trades match this filter.
                            </td>
                          </tr>
                        ) : (
                          filteredTakenTrades.map((trade, index) => {
                            const profitColor =
                              trade.profit > 0
                                ? 'text-green-600 dark:text-green-400'
                                : trade.profit < 0
                                  ? 'text-red-600 dark:text-red-400'
                                  : 'text-muted-foreground';

                            return (
                              <tr
                                key={`${trade.strategy}-${trade.ticker}-${trade.boughtAt}-${index}`}
                                className="border-b border-border/60 hover:bg-muted/30"
                              >
                                <td className="py-2 pr-3 text-xs font-mono uppercase text-primary dark:text-cyan-400">
                                  {trade.strategy}
                                </td>
                                <td className="py-2 pr-3 text-xs font-mono font-bold text-foreground">
                                  {trade.ticker}
                                </td>
                                <td className="py-2 pr-3 text-[10px] font-mono text-muted-foreground whitespace-nowrap">
                                  {trade.boughtAt ? new Date(trade.boughtAt).toLocaleString() : '—'}
                                </td>
                                <td className="py-2 pr-3 text-[10px] font-mono text-muted-foreground whitespace-nowrap">
                                  {trade.soldAt ? new Date(trade.soldAt).toLocaleString() : '—'}
                                </td>
                                <td className="py-2 pr-3 text-xs font-mono text-foreground text-right">
                                  {trade.shares}
                                </td>
                                <td className={`py-2 pr-3 text-xs font-mono font-bold text-right ${profitColor}`}>
                                  ${trade.profit.toFixed(2)}
                                </td>
                                <td className="py-2 text-xs font-mono text-muted-foreground">
                                  {trade.stoppedOut ? 'Yes' : 'No'}
                                </td>
                              </tr>
                            );
                          })
                        )}
                      </tbody>
                    </table>
                  </div>

                  <p className="mt-4 text-[10px] font-mono text-muted-foreground">
                    Trade-universe exploration is coming later.
                  </p>
                </Card>
              </div>
            ) : isProcessing ? (
              <Card className="bg-card border border-border p-12 text-center space-y-4">
                <div className="animate-spin mx-auto w-8 h-8">
                  <RefreshCw className="w-8 h-8 text-primary dark:text-cyan-400" />
                </div>
                <div className="space-y-2">
                  <h3 className="text-sm font-mono uppercase tracking-wide text-primary dark:text-cyan-400">
                    Processing Backtest Results
                  </h3>
                  <p className="text-xs text-muted-foreground max-w-xl mx-auto">
                    Your backtest is executing. Charts and performance metrics will appear here automatically once
                    processing completes.
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

          {backtestEntry && (
            <div className="space-y-4 sticky top-4">
              <Card className="bg-card border border-border p-3">
                <h2 className="text-xs font-mono uppercase tracking-wider text-primary dark:text-cyan-400 mb-3">
                  # Configuration
                </h2>

                <div className="mb-4 pb-3 border-b border-border">
                  <h3 className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground mb-2">
                    # Backtest Info
                  </h3>
                  <div className="space-y-1.5">
                    <div className="flex items-center justify-between">
                      <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">STATUS</span>
                      <span
                        className={`px-1.5 py-0.5 rounded-full text-[9px] font-mono uppercase tracking-wide ${
                          backtestEntry.status === 'Completed'
                            ? 'bg-green-500/10 text-green-600 dark:text-green-400 border border-green-500/30'
                            : backtestEntry.status === 'InProgress'
                              ? 'bg-yellow-500/10 text-yellow-600 dark:text-yellow-400 border border-yellow-500/30'
                              : 'bg-red-500/10 text-red-600 dark:text-red-400 border border-red-500/30'
                        }`}
                      >
                        {backtestEntry.status}
                      </span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">CREDITS</span>
                      <span className="text-xs font-mono font-bold text-primary dark:text-cyan-400">
                        {backtestEntry.creditsUsed?.toFixed(2) || 'N/A'}
                      </span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">DURATION</span>
                      <span className="text-xs font-mono font-bold text-primary dark:text-cyan-400">
                        {formatDuration((backtestEntry as any).durationSeconds)}
                      </span>
                    </div>
                    <div className="flex flex-col gap-0.5 pt-1">
                      <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">CREATED</span>
                      <span className="text-[10px] font-mono text-primary dark:text-cyan-400 leading-tight">
                        {new Date(backtestEntry.createdAt).toLocaleString()}
                      </span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">START</span>
                      <span className="text-[10px] font-mono font-bold text-primary dark:text-cyan-400">
                        {formatDateNoTimezone(backtestEntry.start)}
                      </span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">END</span>
                      <span className="text-[10px] font-mono font-bold text-primary dark:text-cyan-400">
                        {formatDateNoTimezone(backtestEntry.end)}
                      </span>
                    </div>
                  </div>
                </div>

                {(() => {
                  const requestData = getRequestData(backtestEntry);
                  const hasEntryConditions =
                    (requestData.filters && requestData.filters.length > 0) || requestData.argument;

                  if (!hasEntryConditions) return null;

                  return (
                    <div className="mb-4 pb-3 border-b border-border">
                      <h3 className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground mb-2">
                        # Entry Conditions
                      </h3>
                      {requestData.filters && requestData.filters.length > 0 ? (
                        <div className="space-y-1.5">
                          {requestData.filters.map((filter: string, index: number) => (
                            <div
                              key={index}
                              className="font-mono text-[10px] text-primary dark:text-cyan-400 px-2 py-1 bg-muted/30 dark:bg-gray-950/50 border border-border hover:border-primary dark:hover:border-cyan-700 transition-colors"
                            >
                              {'>> '}
                              {filter}
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

                {(() => {
                  const requestData = getRequestData(backtestEntry);
                  const positionInfo = requestData.positionInfo;
                  return (
                    <div className="mb-4 pb-3 border-b border-border">
                      <h3 className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground mb-2">
                        # Position Settings
                      </h3>
                      <div className="space-y-1.5">
                        <div className="flex items-center justify-between">
                          <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">
                            INIT BALANCE
                          </span>
                          <span className="text-xs font-mono font-bold text-primary dark:text-cyan-400">
                            ${positionInfo.startingBalance?.toLocaleString() || 'N/A'}
                          </span>
                        </div>
                        <div className="flex items-center justify-between">
                          <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">
                            POS SIZE
                          </span>
                          <span className="text-xs font-mono font-bold text-primary dark:text-cyan-400">
                            ${positionInfo.positionSize?.toLocaleString() || 'N/A'}
                          </span>
                        </div>
                        <div className="flex items-center justify-between">
                          <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">
                            MAX POS
                          </span>
                          <span className="text-xs font-mono font-bold text-primary dark:text-cyan-400">
                            {positionInfo.maxConcurrentPositions || 'N/A'}
                          </span>
                        </div>
                        <div className="flex items-center justify-between">
                          <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">
                            MODEL TYPE
                          </span>
                          <span className="text-[10px] font-mono font-bold text-primary dark:text-cyan-400">
                            {(positionInfo as any).modelType || 'Fixed'}
                          </span>
                        </div>
                        {(positionInfo as any).allowSimultaneous !== undefined && (
                          <div className="flex items-center justify-between">
                            <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">
                              SIMULTANEOUS
                            </span>
                            <span className="text-[10px] font-mono font-bold text-primary dark:text-cyan-400">
                              {(positionInfo as any).allowSimultaneous ? 'ENABLED' : 'DISABLED'}
                            </span>
                          </div>
                        )}
                      </div>
                    </div>
                  );
                })()}

                {(() => {
                  const requestData = getRequestData(backtestEntry);
                  const exitInfo = requestData.exitInfo;
                  if (
                    !exitInfo.stopLoss &&
                    !exitInfo.profitTarget &&
                    !exitInfo.timeframe &&
                    !(exitInfo as any).avoidOvernight
                  ) {
                    return null;
                  }

                  return (
                    <div>
                      <h3 className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground mb-2">
                        # Exit Strategy
                      </h3>
                      <div className="space-y-1.5">
                        {exitInfo.stopLoss && (
                          <div className="flex items-center justify-between">
                            <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">
                              STOP LOSS
                            </span>
                            <span className="text-[10px] font-mono font-bold text-primary dark:text-cyan-400">
                              {formatStopConfig(exitInfo.stopLoss)}
                            </span>
                          </div>
                        )}
                        {exitInfo.profitTarget && (
                          <div className="flex items-center justify-between">
                            <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">
                              TAKE PROFIT
                            </span>
                            <span className="text-[10px] font-mono font-bold text-primary dark:text-cyan-400">
                              {formatStopConfig(exitInfo.profitTarget)}
                            </span>
                          </div>
                        )}
                        {exitInfo.timeframe && (
                          <div className="flex items-center justify-between">
                            <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">
                              TIMED EXIT
                            </span>
                            <span className="text-[10px] font-mono font-bold text-primary dark:text-cyan-400">
                              {formatTimeframe(exitInfo.timeframe)}
                            </span>
                          </div>
                        )}
                        {(exitInfo as any).avoidOvernight !== undefined && (
                          <div className="flex items-center justify-between">
                            <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">
                              AVOID OVERNIGHT
                            </span>
                            <span className="text-[10px] font-mono font-bold text-primary dark:text-cyan-400">
                              {(exitInfo as any).avoidOvernight ? 'ENABLED' : 'DISABLED'}
                            </span>
                          </div>
                        )}
                      </div>
                    </div>
                  );
                })()}
              </Card>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

export default BacktestDetailPage;
