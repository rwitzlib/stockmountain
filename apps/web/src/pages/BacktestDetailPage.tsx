import { useState, useEffect, useMemo, useCallback } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { FilterDisplay } from '../components/backtest/FilterDisplay';
import { BacktestReport, BenchmarkBar, RailRow } from '../components/backtest/BacktestReport';
import { ShareDialog } from '../components/backtest/ShareDialog';
import { backtestApi } from '../api/backtestApi';
import { TradingData } from '../types/types';
import { BacktestEntry, BacktestRequest } from '../types/backtest';
import {
  Exit,
  ExitCandleType,
  PositionType,
  PriceActionType,
  ScanArgument,
  Strategy,
  Timespan,
} from '../types/strategy';
import { Button } from '../components/ui/button';
import { Card } from '../components/ui/card';
import { ArrowLeft, RefreshCw, Copy, Bot, Share2 } from 'lucide-react';
import { formatDateNoTimezone } from '../utils/dateFormatter';
import { normalizeTradingData } from '../utils/backtestNormalize';
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

export function BacktestDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [data, setData] = useState<BacktestDetailData | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [isPolling, setIsPolling] = useState(false);
  const [shareOpen, setShareOpen] = useState(false);

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

  // Dumb 1:1 copy of the backtest config onto the strategy contract (plan 08 phase 5):
  // any divergence between what was backtested and what goes live is a bug factory.
  const mapBacktestToStrategy = (backtestEntry: BacktestEntry): Strategy => {
    const requestData = getRequestData(backtestEntry);
    const { positionInfo, exitInfo } = requestData;

    const toExit = (config: StopConfigView | undefined): Exit | undefined =>
      config?.value == null
        ? undefined
        : {
            candleType: (config.candleType as ExitCandleType) ?? 'CurrentCandle',
            priceActionType: (config.priceActionType as PriceActionType) ?? 'close',
            type: config.type === 'flat' || config.type === 'value' ? 'flat' : 'percent',
            value: config.value,
          };

    return {
      name: `Backtest ${backtestEntry.id.slice(0, 8)}`,
      state: 'Inactive',
      visibility: 'Private',
      type: 'Paper',
      integration: 'Default',
      positionSettings: {
        startingBalance: positionInfo.startingBalance ?? 10000,
        maxConcurrentPositions: positionInfo.maxConcurrentPositions ?? 1,
        allowSimultaneous:
          positionInfo.allowSimultaneous ?? (positionInfo.maxConcurrentPositions ?? 1) > 1,
        model: {
          type: (positionInfo.modelType as PositionType) ?? 'Fixed',
          size: positionInfo.positionSize ?? 1000,
        },
      },
      exitSettings: {
        stopLoss: toExit(exitInfo.stopLoss),
        takeProfit: toExit(exitInfo.profitTarget),
        timedExit: exitInfo.timeframe
          ? {
              avoidOvernight: exitInfo.avoidOvernight ?? true,
              timeframe: {
                multiplier: exitInfo.timeframe.multiplier ?? 1,
                timespan: (exitInfo.timeframe.timespan as Timespan) ?? 'minute',
              },
            }
          : undefined,
      },
      entrySettings: {
        filters: requestData.filters || [],
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

  // BacktestReport aligns/scales the benchmark; here we only reshape polygon bars to day+close.
  const benchmarkBars = useMemo<BenchmarkBar[] | null>(() => {
    if (!spyDataResponse?.results?.length) return null;

    return [...spyDataResponse.results]
      .sort((a, b) => a.t - b.t)
      .map((bar) => ({
        day: new Date(bar.t).toISOString().slice(0, 10),
        close: bar.c,
      }));
  }, [spyDataResponse]);

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
  const tradingDays = data.tradingData?.hold.equity.length ?? 0;
  const totalTrades =
    data.tradingData?.hold.stats.totalTradesTaken ?? data.tradingData?.hold.trades.length ?? 0;

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
            <Button
              variant="outline"
              size="sm"
              onClick={() => setShareOpen(true)}
              disabled={!data.tradingData || backtestEntry.status !== 'Completed'}
            >
              <Share2 className="mr-1.5 h-4 w-4" />
              Share
            </Button>
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

        <ShareDialog
          backtestId={backtestEntry.id}
          open={shareOpen}
          onOpenChange={setShareOpen}
        />

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

        {data.tradingData ? (
          <BacktestReport
            tradingData={data.tradingData}
            startingBalance={startingBalance}
            benchmarkBars={benchmarkBars}
            configRail={
              requestData ? (
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
              ) : undefined
            }
          />
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
