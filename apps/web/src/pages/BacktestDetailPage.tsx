import { useState, useEffect, useMemo, useCallback } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { FilterDisplay } from '../components/backtest/FilterDisplay';
import { EquityCurveCard, EquitySeriesDef } from '../components/backtest/charts/EquityCurveCard';
import { DailyPnlChart } from '../components/backtest/charts/DailyPnlChart';
import { HistogramChart } from '../components/backtest/charts/HistogramChart';
import { EntryTimingPanel } from '../components/backtest/EntryTimingPanel';
import { ExitReasonPanel } from '../components/backtest/ExitReasonPanel';
import { TickerLeadersPanel } from '../components/backtest/TickerLeadersPanel';
import { BacktestTradesTable } from '../components/backtest/BacktestTradesTable';
import { backtestApi } from '../api/backtestApi';
import {
  TradingData,
  TradeStrategy,
  StrategyPortfolio,
  ExecutedTrade,
  EquityPoint,
} from '../types/types';
import { BacktestEntry, BacktestRequest } from '../types/backtest';
import { ScanArgument, Strategy } from '../types/strategy';
import { Button } from '../components/ui/button';
import { Card } from '../components/ui/card';
import { ArrowLeft, RefreshCw, Copy, Bot } from 'lucide-react';
import { formatDateNoTimezone } from '../utils/dateFormatter';
import {
  formatAxisCurrency,
  formatCurrency,
  formatSignedCurrency,
  formatSignedPercent,
} from '../utils/formatters';
import {
  computeAverageExitEfficiency,
  computeDerivedTradeStats,
  computeDrawdown,
  computeDurationHistogram,
  computeEntryTimeBuckets,
  computeExitReasonBreakdown,
  computeProfitHistogram,
  computeTickerAggregates,
} from '../utils/backtestAnalytics';
import { toast } from '../hooks/use-toast';
import { fetchMarketData } from '../services/massive';
import { useQuery } from '@tanstack/react-query';

interface BacktestDetailData {
  tradingData: TradingData | null;
  backtestEntry: BacktestEntry;
  isProcessing: boolean;
}

interface StopConfigView {
  candleType?: string;
  priceActionType?: string;
  type?: string;
  value?: number;
}

/** Request settings normalized across the current `request` and legacy `requestDetails` shapes */
interface RequestDataView {
  positionInfo: {
    startingBalance?: number;
    maxConcurrentPositions?: number;
    positionSize?: number;
    allowSimultaneous?: boolean;
    modelType?: string;
  };
  exitInfo: {
    stopLoss?: StopConfigView;
    profitTarget?: StopConfigView;
    timeframe?: { multiplier?: number; timespan?: string };
    avoidOvernight?: boolean;
  };
  argument?: ScanArgument;
  filters: string[];
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
      maxRunup: t.maxRunup != null && Number.isFinite(Number(t.maxRunup))
        ? Number(t.maxRunup)
        : undefined,
      maxDrawdown: t.maxDrawdown != null && Number.isFinite(Number(t.maxDrawdown))
        ? Number(t.maxDrawdown)
        : undefined,
      stoppedOut: Boolean(t.stoppedOut),
      exitReason: typeof t.exitReason === 'string' ? (t.exitReason as ExecutedTrade['exitReason']) : undefined,
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

function KpiTile({ label, value, sub, valueColor }: {
  label: string;
  value: string;
  sub?: string;
  valueColor?: string;
}) {
  return (
    <Card className="p-3.5">
      <div className="mb-0.5 whitespace-nowrap text-[10.5px] uppercase tracking-widest text-muted-foreground">
        {label}
      </div>
      <div className="text-[22px] font-semibold leading-tight tabular-nums" style={{ color: valueColor }}>
        {value}
      </div>
      {sub && <div className="mt-0.5 text-[11.5px] text-muted-foreground tabular-nums">{sub}</div>}
    </Card>
  );
}

function RailRow({ label, value, valueColor }: { label: string; value: string; valueColor?: string }) {
  return (
    <div className="flex items-baseline justify-between border-b border-border/60 py-1.5 text-[13px] last:border-b-0">
      <span className="text-muted-foreground">{label}</span>
      <span className="font-semibold tabular-nums" style={{ color: valueColor }}>{value}</span>
    </div>
  );
}

export function BacktestDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [data, setData] = useState<BacktestDetailData | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [isPolling, setIsPolling] = useState(false);

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

  const getRequestData = useCallback((backtestEntry: BacktestEntry): RequestDataView => {
    if (backtestEntry.request) {
      const req = backtestEntry.request;
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
        // Expression filters carried as a legacy ScanArgument for the create-strategy flow
        argument: req.entrySettings?.filters
          ? ({
              operator: 'AND' as const,
              filters: req.entrySettings.filters.map((f: string) => ({ expression: f })),
            } as unknown as ScanArgument)
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
        exitInfo: backtestEntry.requestDetails.exitInfo ?? {},
        argument: backtestEntry.requestDetails.argument,
        filters: [],
      };
    }

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

  const formatStopConfig = (config: StopConfigView | undefined) => {
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

  const formatTimeframe = (timeframe: RequestDataView['exitInfo']['timeframe']) => {
    if (!timeframe) return 'Not set';
    return `${timeframe.multiplier} ${timeframe.timespan}${(timeframe.multiplier ?? 1) > 1 ? 's' : ''}`;
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
        AllowSimultaneous: positionInfo.allowSimultaneous ?? ((positionInfo.maxConcurrentPositions ?? 1) > 1),
        MaxConcurrentPositions: positionInfo.maxConcurrentPositions ?? 1,
        Model: {
          Type: positionInfo.modelType ?? 'Fixed',
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

  const requestData = data?.backtestEntry ? getRequestData(data.backtestEntry) : null;
  const startingBalance = requestData?.positionInfo.startingBalance || 10000;

  const analytics = useMemo(() => {
    if (!data?.tradingData) return null;

    const primary = data.tradingData.hold;
    const ceiling = data.tradingData.high;
    const netProfit = primary.stats.sumProfit ?? primary.stats.balanceChange ?? 0;
    const ceilingProfit = ceiling.stats.sumProfit ?? ceiling.stats.balanceChange ?? 0;

    return {
      primary,
      ceiling,
      netProfit,
      netPct: (netProfit / startingBalance) * 100,
      ceilingPct: (ceilingProfit / startingBalance) * 100,
      exitEfficiency: ceilingProfit > 0 ? (netProfit / ceilingProfit) * 100 : null,
      averageTradeExitEfficiency: computeAverageExitEfficiency(primary.trades),
      derived: computeDerivedTradeStats(primary.trades),
      drawdown: computeDrawdown(primary.equity),
      profitHist: computeProfitHistogram(primary.trades),
      durationHist: computeDurationHistogram(primary.trades),
      entryBuckets: computeEntryTimeBuckets(primary.trades),
      tickers: computeTickerAggregates(primary.trades),
      exitReasons: computeExitReasonBreakdown(primary.trades),
    };
  }, [data?.tradingData, startingBalance]);

  const spySeries = useMemo(() => {
    if (!analytics || !spyDataResponse?.results?.length) return null;

    const bars = [...spyDataResponse.results].sort((a, b) => a.t - b.t);
    const firstClose = bars[0].c;
    if (!firstClose) return null;

    const balances = analytics.primary.equity.map((pt) => {
      const day = pt.date.slice(0, 10);
      let close: number | null = null;
      for (const bar of bars) {
        if (new Date(bar.t).toISOString().slice(0, 10) <= day) {
          close = bar.c;
        } else {
          break;
        }
      }
      return close != null ? startingBalance * (close / firstClose) : null;
    });

    const lastClose = bars[bars.length - 1].c;
    return { balances, pct: (lastClose / firstClose - 1) * 100 };
  }, [analytics, spyDataResponse, startingBalance]);

  if (isLoading) {
    return (
      <div className="min-h-screen bg-background p-4 pt-20 text-foreground md:p-8 md:pt-8">
        <div className="mx-auto max-w-[1240px]">
          <Card className="p-8 text-center">
            <div className="mb-2 text-xs uppercase tracking-widest text-muted-foreground">Loading</div>
            <div className="text-base">Fetching backtest details…</div>
          </Card>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="min-h-screen bg-background p-4 pt-20 text-foreground md:p-8 md:pt-8">
        <div className="mx-auto max-w-[1240px] space-y-6">
          <div className="flex items-center gap-4">
            <Link to="/backtest">
              <Button variant="outline" size="sm">
                <ArrowLeft className="mr-1 h-4 w-4" />
                Back
              </Button>
            </Link>
            <h1 className="text-xl font-semibold">Backtest details</h1>
          </div>
          <Card className="border-destructive/30 bg-destructive/10 p-6">
            <div className="text-sm text-destructive">{error}</div>
          </Card>
        </div>
      </div>
    );
  }

  if (!data) {
    return (
      <div className="min-h-screen bg-background p-4 pt-20 text-foreground md:p-8 md:pt-8">
        <div className="mx-auto max-w-[1240px]">
          <Card className="p-8 text-center">
            <div className="text-base">No backtest data found.</div>
          </Card>
        </div>
      </div>
    );
  }

  const { backtestEntry, isProcessing } = data;
  const stats = analytics?.primary.stats;
  const derived = analytics?.derived;
  const tradingDays = analytics?.primary.equity.length ?? 0;
  const totalTrades = stats?.totalTradesTaken ?? analytics?.primary.trades.length ?? 0;

  const equitySeries: EquitySeriesDef[] = analytics
    ? [
        {
          key: 'strategy',
          name: 'Strategy',
          color: 'var(--chart-strategy)',
          area: true,
          balances: analytics.primary.equity.map((pt) => pt.totalBalance),
        },
        ...(spySeries
          ? [
              {
                key: 'spy',
                name: 'SPY',
                color: 'var(--chart-benchmark)',
                dashed: true,
                balances: spySeries.balances,
              },
            ]
          : []),
        ...(analytics.ceiling.equity.length > 0
          ? [
              {
                key: 'ceiling',
                name: 'Max potential',
                color: 'var(--chart-ceiling)',
                defaultHidden: true,
                hiddenHint: formatSignedPercent(analytics.ceilingPct, 0),
                balances: analytics.ceiling.equity.map((pt) => pt.totalBalance),
              },
            ]
          : []),
      ]
    : [];

  const statusStyle =
    backtestEntry.status === 'Completed'
      ? 'text-green-600 dark:text-green-400 bg-green-500/10'
      : backtestEntry.status === 'Failed'
        ? 'text-red-600 dark:text-red-400 bg-red-500/10'
        : 'text-yellow-600 dark:text-yellow-400 bg-yellow-500/10';

  return (
    <div className="min-h-screen bg-background p-4 pt-20 text-foreground md:p-8 md:pt-8">
      <div className="mx-auto max-w-[1240px]">
        {/* ---------- Masthead ---------- */}
        <header className="mb-6 flex flex-wrap items-end gap-4 border-b-2 border-foreground/80 pb-5">
          <div>
            <div className="mb-1.5 flex items-center gap-3">
              <Link
                to="/backtest"
                className="inline-flex items-center gap-1 text-xs uppercase tracking-widest text-muted-foreground transition-colors hover:text-foreground"
              >
                <ArrowLeft className="h-3.5 w-3.5" />
                Backtests
              </Link>
              <span
                className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-[11px] font-semibold uppercase tracking-wide ${statusStyle}`}
              >
                <span className="h-1.5 w-1.5 rounded-full bg-current" />
                {backtestEntry.status}
              </span>
              {isPolling && (
                <span className="text-[11px] uppercase tracking-widest text-muted-foreground">
                  Polling…
                </span>
              )}
            </div>
            <h1 className="text-2xl font-semibold tracking-tight md:text-3xl">
              Backtest report{' '}
              <span className="font-mono text-xl font-normal text-muted-foreground md:text-2xl">
                {backtestEntry.id.slice(0, 8)}
              </span>
            </h1>
            <p className="mt-1 text-[13px] text-muted-foreground tabular-nums">
              {formatDateNoTimezone(backtestEntry.start)} – {formatDateNoTimezone(backtestEntry.end)}
              {tradingDays > 0 && (
                <>
                  {' '}· <b className="font-semibold text-foreground">{tradingDays} trading days</b>
                </>
              )}
              {totalTrades > 0 && <> · {totalTrades.toLocaleString()} trades</>}
              {backtestEntry.creditsUsed != null && (
                <> · {Math.round(backtestEntry.creditsUsed).toLocaleString()} credits</>
              )}
            </p>
          </div>
          <div className="ml-auto flex gap-2 pb-1">
            <Button variant="outline" size="sm" onClick={handleCopyBacktest}>
              <Copy className="mr-1.5 h-4 w-4" />
              Copy setup
            </Button>
            <Button size="sm" onClick={handleCreateStrategy}>
              <Bot className="mr-1.5 h-4 w-4" />
              Create strategy
            </Button>
          </div>
        </header>

        {/* ---------- Processing banner ---------- */}
        {isProcessing && (
          <Card className="mb-6 border-yellow-500/30 bg-yellow-500/10 p-4">
            <div className="flex flex-wrap items-center justify-between gap-4">
              <div className="flex items-start gap-3">
                <RefreshCw className="mt-1 h-5 w-5 animate-spin text-yellow-600 dark:text-yellow-400" />
                <div>
                  <h3 className="text-sm font-semibold text-yellow-700 dark:text-yellow-300">
                    Backtest processing
                  </h3>
                  <p className="text-sm text-yellow-700/90 dark:text-yellow-300/90">
                    This typically takes 2–5 minutes. Results appear automatically when complete.
                  </p>
                </div>
              </div>
              <Button variant="outline" size="sm" onClick={handleRefreshResults}>
                <RefreshCw className="mr-1 h-4 w-4" />
                Check now
              </Button>
            </div>
          </Card>
        )}

        {analytics && stats ? (
          <>
            {/* ---------- Verdict ---------- */}
            <section className="mb-6 grid grid-cols-1 gap-4 lg:grid-cols-[minmax(300px,1.1fr)_2fr]">
              <Card className="flex flex-col justify-center p-6">
                <div className="text-[11.5px] uppercase tracking-widest text-muted-foreground">
                  Net return
                </div>
                <div
                  className="my-1 text-5xl font-semibold leading-none tracking-tight tabular-nums md:text-6xl"
                  style={{
                    color:
                      analytics.netProfit > 0
                        ? 'var(--chart-gain)'
                        : analytics.netProfit < 0
                          ? 'var(--chart-loss)'
                          : undefined,
                  }}
                >
                  {formatSignedPercent(analytics.netPct)}
                </div>
                <div className="text-sm text-muted-foreground tabular-nums">
                  <b className="font-semibold text-foreground">{formatSignedCurrency(analytics.netProfit)}</b>{' '}
                  on {formatAxisCurrency(startingBalance)} starting balance
                </div>
                <div className="mt-3.5 flex flex-wrap gap-2 text-xs tabular-nums">
                  {spySeries && (
                    <span className="rounded-full bg-muted px-2.5 py-1 text-muted-foreground">
                      vs SPY{' '}
                      <b
                        className="font-semibold"
                        style={{
                          color:
                            analytics.netPct >= spySeries.pct
                              ? 'var(--chart-gain)'
                              : 'var(--chart-loss)',
                        }}
                      >
                        {formatSignedPercent(analytics.netPct - spySeries.pct, 1).replace('%', '')}pts
                      </b>
                    </span>
                  )}
                  {analytics.ceilingPct > 0 && (
                    <span className="rounded-full bg-muted px-2.5 py-1 text-muted-foreground">
                      Max potential ceiling{' '}
                      <b className="font-semibold text-foreground">
                        {formatSignedPercent(analytics.ceilingPct, 0)}
                      </b>
                    </span>
                  )}
                  {analytics.exitEfficiency != null && (
                    <span className="rounded-full bg-muted px-2.5 py-1 text-muted-foreground">
                      Exit efficiency{' '}
                      <b className="font-semibold text-foreground">
                        {analytics.exitEfficiency.toFixed(1)}%
                      </b>
                    </span>
                  )}
                </div>
              </Card>

              <div className="grid grid-cols-2 gap-2.5 md:grid-cols-4">
                <KpiTile
                  label="Win rate"
                  value={`${(stats.winRatio * 100).toFixed(1)}%`}
                  sub={derived ? `${derived.wins.toLocaleString()} W · ${derived.losses.toLocaleString()} L` : undefined}
                />
                <KpiTile
                  label="Profit factor"
                  value={stats.profitFactor != null && Number.isFinite(stats.profitFactor) ? stats.profitFactor.toFixed(2) : '—'}
                  sub={`avg win ${formatCurrency(stats.avgWin)} · loss ${formatCurrency(stats.avgLoss)}`}
                />
                <KpiTile
                  label="Sharpe"
                  value={stats.sharpeRatio != null ? stats.sharpeRatio.toFixed(2) : '—'}
                  sub={stats.dailyReturnStdDev != null ? `daily σ ${(stats.dailyReturnStdDev * 100).toFixed(2)}%` : undefined}
                />
                <KpiTile
                  label="Max drawdown"
                  value={stats.maxDrawdown != null ? `−${(stats.maxDrawdown * 100).toFixed(2)}%` : '—'}
                  sub="peak to trough"
                  valueColor={stats.maxDrawdown ? 'var(--chart-loss)' : undefined}
                />
                <KpiTile
                  label="Expectancy"
                  value={derived ? formatCurrency(derived.expectancy) : '—'}
                  sub="per trade"
                />
                <KpiTile
                  label="Median hold"
                  value={derived ? `${Math.round(derived.medianHoldMinutes)}m` : '—'}
                  sub={derived ? `${Math.round(derived.fullHoldPct * 100)}% held full window` : undefined}
                />
                <KpiTile
                  label="Universe"
                  value={derived ? derived.uniqueTickers.toLocaleString() : '—'}
                  sub="tickers traded"
                />
                <KpiTile
                  label="Streaks"
                  value={derived ? `${derived.winStreak} / ${derived.lossStreak}` : '—'}
                  sub="longest win / loss run"
                />
              </div>
            </section>

            {/* ---------- Equity + config ---------- */}
            <div className="mb-6 grid grid-cols-1 items-start gap-4 lg:grid-cols-[minmax(0,1fr)_300px]">
              <EquityCurveCard
                dates={analytics.primary.equity.map((pt) => pt.date.slice(0, 10))}
                series={equitySeries}
                startingBalance={startingBalance}
                equity={analytics.primary.equity}
                drawdown={analytics.drawdown}
                footnote="Max potential assumes every position is sold at its in-trade high — an upper bound on what better exits could capture, not a peer strategy."
              />

              {requestData && (
                <aside className="flex flex-col gap-4 self-start lg:sticky lg:top-4">
                  {(requestData.filters.length > 0 || requestData.argument) && (
                    <Card className="p-4">
                      <h3 className="mb-2 text-[11px] uppercase tracking-widest text-muted-foreground">
                        Entry filters
                      </h3>
                      {requestData.filters.length > 0 ? (
                        <div className="flex flex-col gap-1.5">
                          {requestData.filters.map((filter: string, index: number) => (
                            <code
                              key={index}
                              className="rounded-md border border-border/60 bg-muted/50 px-2.5 py-1.5 font-mono text-xs"
                            >
                              {filter}
                            </code>
                          ))}
                        </div>
                      ) : requestData.argument ? (
                        <FilterDisplay argument={requestData.argument} />
                      ) : null}
                    </Card>
                  )}

                  <Card className="p-4">
                    <h3 className="mb-1 text-[11px] uppercase tracking-widest text-muted-foreground">
                      Position
                    </h3>
                    <RailRow
                      label="Starting balance"
                      value={`$${(requestData.positionInfo.startingBalance ?? 0).toLocaleString()}`}
                    />
                    <RailRow
                      label="Position size"
                      value={`$${(requestData.positionInfo.positionSize ?? 0).toLocaleString()} ${(requestData.positionInfo.modelType ?? 'Fixed').toLowerCase()}`}
                    />
                    <RailRow
                      label="Max concurrent"
                      value={String(requestData.positionInfo.maxConcurrentPositions ?? '—')}
                    />
                    {requestData.positionInfo.allowSimultaneous !== undefined && (
                      <RailRow
                        label="Simultaneous entries"
                        value={requestData.positionInfo.allowSimultaneous ? 'Allowed' : 'Not allowed'}
                      />
                    )}
                  </Card>

                  {(requestData.exitInfo.stopLoss ||
                    requestData.exitInfo.profitTarget ||
                    requestData.exitInfo.timeframe ||
                    requestData.exitInfo.avoidOvernight !== undefined) && (
                    <Card className="p-4">
                      <h3 className="mb-1 text-[11px] uppercase tracking-widest text-muted-foreground">
                        Exits
                      </h3>
                      {requestData.exitInfo.stopLoss && (
                        <RailRow
                          label="Stop loss"
                          value={formatStopConfig(requestData.exitInfo.stopLoss)}
                          valueColor="var(--chart-loss)"
                        />
                      )}
                      {requestData.exitInfo.profitTarget && (
                        <RailRow
                          label="Take profit"
                          value={formatStopConfig(requestData.exitInfo.profitTarget)}
                          valueColor="var(--chart-gain)"
                        />
                      )}
                      {requestData.exitInfo.timeframe && (
                        <RailRow label="Timed exit" value={formatTimeframe(requestData.exitInfo.timeframe)} />
                      )}
                      {requestData.exitInfo.avoidOvernight !== undefined && (
                        <RailRow
                          label="Overnight"
                          value={requestData.exitInfo.avoidOvernight ? 'Avoided' : 'Allowed'}
                        />
                      )}
                    </Card>
                  )}

                  <Card className="p-4">
                    <h3 className="mb-1 text-[11px] uppercase tracking-widest text-muted-foreground">
                      Run
                    </h3>
                    <RailRow
                      label="Credits used"
                      value={backtestEntry.creditsUsed != null ? backtestEntry.creditsUsed.toFixed(2) : 'N/A'}
                    />
                    <RailRow label="Duration" value={formatDuration(backtestEntry.durationSeconds)} />
                    <RailRow label="Created" value={new Date(backtestEntry.createdAt).toLocaleString()} />
                  </Card>
                </aside>
              )}
            </div>

            {/* ---------- Insights ---------- */}
            <div className="mb-3.5 mt-8 flex flex-wrap items-baseline gap-3">
              <h2 className="text-lg font-semibold tracking-tight">Where the edge lives</h2>
              <span className="text-[13px] text-muted-foreground">
                computed from the {totalTrades.toLocaleString()} strategy trades
              </span>
            </div>

            <div className="mb-4 grid grid-cols-1 gap-4 lg:grid-cols-2">
              <Card className="p-4 md:p-5">
                <h2 className="mb-2 text-sm font-semibold">P&L by entry time</h2>
                <EntryTimingPanel buckets={analytics.entryBuckets} />
              </Card>
              <Card className="p-4 md:p-5">
                <h2 className="mb-2 text-sm font-semibold">Daily P&L</h2>
                <DailyPnlChart equity={analytics.primary.equity} />
              </Card>
            </div>

            <div className="mb-4 grid grid-cols-1 gap-4 lg:grid-cols-2">
              <Card className="p-4 md:p-5">
                <h2 className="mb-2 text-sm font-semibold">Trade P&L distribution</h2>
                <HistogramChart
                  histogram={analytics.profitHist}
                  colorFor={(x0) => (x0 >= 0 ? 'var(--chart-gain)' : 'var(--chart-loss)')}
                  formatX={formatAxisCurrency}
                  ariaLabel="Histogram of per-trade profit"
                />
                {derived && (
                  <p className="mt-2 text-xs text-muted-foreground tabular-nums">
                    Best trade {formatSignedCurrency(derived.bestTrade)} · worst{' '}
                    {formatSignedCurrency(derived.worstTrade)}
                    {analytics.averageTradeExitEfficiency != null && (
                      <>
                        {' '}· average exit efficiency{' '}
                        {formatSignedPercent(analytics.averageTradeExitEfficiency * 100, 0)}
                      </>
                    )}
                    {' '}— the exit walls shape this book.
                  </p>
                )}
              </Card>
              <Card className="p-4 md:p-5">
                <h2 className="mb-2 text-sm font-semibold">Time in trade</h2>
                <HistogramChart
                  histogram={analytics.durationHist}
                  colorFor={() => 'var(--chart-strategy)'}
                  formatX={(x) => `${x}m`}
                  ariaLabel="Histogram of trade duration in minutes"
                />
                {derived && (
                  <p className="mt-2 text-xs text-muted-foreground tabular-nums">
                    {Math.round(derived.fullHoldPct * 100)}% of trades ride untouched to the{' '}
                    {Math.round(derived.maxHoldMinutes)}-minute exit window.
                  </p>
                )}
              </Card>
            </div>

            <div className="mb-6 grid grid-cols-1 gap-4 lg:grid-cols-2">
              <Card className="p-4 md:p-5">
                <h2 className="mb-2 text-sm font-semibold">P&L by exit reason</h2>
                <ExitReasonPanel breakdown={analytics.exitReasons} />
              </Card>
              <Card className="p-4 md:p-5">
                <h2 className="-mb-6 text-sm font-semibold">Tickers</h2>
                <TickerLeadersPanel best={analytics.tickers.best} worst={analytics.tickers.worst} />
              </Card>
            </div>

            {/* ---------- Trades ---------- */}
            <div className="mb-3.5 flex flex-wrap items-baseline gap-3">
              <h2 className="text-lg font-semibold tracking-tight">Trades</h2>
              <span className="text-[13px] text-muted-foreground">
                strategy exits · most recent first
              </span>
            </div>
            <Card className="p-4 md:p-5">
              <BacktestTradesTable trades={analytics.primary.trades} />
            </Card>
          </>
        ) : isProcessing ? (
          <Card className="space-y-4 p-12 text-center">
            <RefreshCw className="mx-auto h-8 w-8 animate-spin text-muted-foreground" />
            <div className="space-y-1">
              <h3 className="text-sm font-semibold">Processing backtest results</h3>
              <p className="mx-auto max-w-xl text-sm text-muted-foreground">
                Charts and performance metrics will appear here automatically once processing
                completes. Estimated completion: 2–5 minutes.
              </p>
            </div>
          </Card>
        ) : (
          <Card className="space-y-2 p-12 text-center">
            <h3 className="text-sm font-semibold">No results available</h3>
            <p className="text-sm text-muted-foreground">
              Results for this backtest are not currently available.
            </p>
          </Card>
        )}
      </div>
    </div>
  );
}

export default BacktestDetailPage;
