import { getAuthHeaders } from '../../api/authToken';
import { API_BASE_URL } from '../../api/apiConfig';
import { useMemo } from 'react';
import { useParams, useNavigate, useLocation } from 'react-router-dom';
import { useQuery, useMutation } from '@tanstack/react-query';
import { Trade } from '../../types/trade';
import { Exit, Timeframe } from '../../types/strategy';
import { Button } from '../../components/ui/button';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle, AlertDialogTrigger } from '../../components/ui/alert-dialog';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '../../components/ui/dropdown-menu';
import { ArrowLeft, Trash2, MoreVertical, Copy, Settings, Beaker } from 'lucide-react';
import { toast } from '../../hooks/use-toast';
import { strategyApi } from '../../api/strategyApi';
import { Badge } from '../../components/ui/badge';
import { Card } from '../../components/ui/card';
import { BacktestReport, BenchmarkBar, RailRow } from '../../components/backtest/BacktestReport';
import { buildLiveTradingData } from '../../utils/liveTradingData';
import { formatCurrency } from '../../utils/formatters';
import { fetchMarketData } from '../../services/massive';
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

function formatExitConfig(config: Exit | undefined): string {
  if (!config) return 'Not set';
  const priceAction = config.priceActionType ? `${config.priceActionType} ` : '';
  return config.type === 'percent' ? `${priceAction}${config.value}%` : `${priceAction}$${config.value}`;
}

function formatTimeframe(timeframe: Timeframe | undefined): string {
  if (!timeframe) return 'Not set';
  return `${timeframe.multiplier} ${timeframe.timespan}${timeframe.multiplier > 1 ? 's' : ''}`;
}

function formatOpenedAt(iso: string): string {
  const date = new Date(iso);
  return Number.isNaN(date.getTime())
    ? iso
    : date.toLocaleString('en-US', {
        month: 'short',
        day: 'numeric',
        hour: 'numeric',
        minute: '2-digit',
        timeZone: 'America/New_York',
      });
}

const StrategyDetailPage = () => {
  const { strategyId } = useParams<{ strategyId: string }>();
  const navigate = useNavigate();
  const location = useLocation();

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
      const response = await fetch(`${API_BASE_URL}/trade?strategy=${strategyId}`, {
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

  const trades = useMemo(() => tradesResponse?.trades ?? [], [tradesResponse]);
  const openTrades = useMemo(() => trades.filter((t) => !t.closedAt), [trades]);

  const positionSettings = strategy?.positionSettings ?? {
    startingBalance: 1000,
    allowSimultaneous: false,
    maxConcurrentPositions: 1,
    model: {
      type: 'Fixed' as const,
      size: 100
    }
  };
  const startingBalance = positionSettings.startingBalance;

  // Reshape closed live trades into the backtest result contract for BacktestReport
  const tradingData = useMemo(
    () => buildLiveTradingData(trades, startingBalance),
    [trades, startingBalance]
  );

  // Fetch SPY daily closes across the traded range for the benchmark overlay
  const { data: spyDataResponse } = useQuery({
    queryKey: ['spyData', strategyId, tradingData?.hold.equity.length],
    queryFn: async () => {
      const firstDay = tradingData!.hold.equity[0].date;
      const toDate = new Date().toISOString().split('T')[0];

      try {
        return await fetchMarketData({
          ticker: 'SPY',
          multiplier: 1,
          timespan: 'day',
          from: firstDay,
          to: toDate,
        });
      } catch (error) {
        console.error('Error fetching SPY data:', error);
        return null;
      }
    },
    enabled: !!strategyId && !!tradingData && tradingData.hold.equity.length > 0,
  });

  // BacktestReport aligns/scales the benchmark; here we only reshape bars to day+close.
  const benchmarkBars = useMemo<BenchmarkBar[] | null>(() => {
    if (!spyDataResponse?.results?.length) return null;

    return [...spyDataResponse.results]
      .sort((a, b) => a.t - b.t)
      .map((bar) => ({
        day: new Date(bar.t).toISOString().slice(0, 10),
        close: bar.c,
      }));
  }, [spyDataResponse]);

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

  // Determine the correct back navigation path
  const getBackNavigationPath = () => {
    if (location.state?.from) {
      return location.state.from;
    }

    const referrer = document.referrer;
    if (referrer.includes('/optimus/public-dashboard')) {
      return '/optimus/public-dashboard';
    }

    return '/optimus/dashboard';
  };

  if (isLoadingStrategy) {
    return (
      <div className="min-h-screen bg-background p-4 pt-20 text-foreground md:p-8 md:pt-8">
        <div className="mx-auto max-w-[1240px]">
          <Card className="p-8 text-center">
            <div className="mb-2 text-xs uppercase tracking-widest text-muted-foreground">Loading</div>
            <div className="text-base">Fetching strategy details…</div>
          </Card>
        </div>
      </div>
    );
  }

  if (!strategy) {
    return (
      <div className="min-h-screen bg-background p-4 pt-20 text-foreground md:p-8 md:pt-8">
        <div className="mx-auto max-w-[1240px]">
          <Card className="p-8 text-center">
            <div className="text-base font-medium text-destructive dark:text-red-400">Strategy not found</div>
            <div className="mt-2 text-xs text-muted-foreground">Invalid strategy ID or access denied</div>
          </Card>
        </div>
      </div>
    );
  }

  const { exitSettings, entrySettings } = strategy;
  const closedCount = tradingData?.hold.trades.length ?? 0;

  const configRail = (
    <aside className="flex flex-col gap-4 self-start lg:sticky lg:top-4">
      {(entrySettings?.filters?.length ?? 0) > 0 && (
        <Card className="p-4">
          <h3 className="mb-2 text-[11px] uppercase tracking-widest text-muted-foreground">
            Entry filters
          </h3>
          <div className="flex flex-col gap-1.5">
            {entrySettings.filters.map((filter, index) => (
              <code
                key={index}
                className="rounded-md border border-border/60 bg-muted/50 px-2.5 py-1.5 font-mono text-xs"
              >
                {filter}
              </code>
            ))}
          </div>
        </Card>
      )}

      <Card className="p-4">
        <h3 className="mb-1 text-[11px] uppercase tracking-widest text-muted-foreground">
          Position
        </h3>
        <RailRow label="Starting balance" value={`$${startingBalance.toLocaleString()}`} />
        <RailRow
          label="Position size"
          value={`$${positionSettings.model.size.toLocaleString()} ${positionSettings.model.type.toLowerCase()}`}
        />
        <RailRow label="Max concurrent" value={String(positionSettings.maxConcurrentPositions)} />
        <RailRow
          label="Simultaneous entries"
          value={positionSettings.allowSimultaneous ? 'Allowed' : 'Not allowed'}
        />
        {positionSettings.cooldown && (
          <RailRow label="Cooldown" value={formatTimeframe(positionSettings.cooldown)} />
        )}
      </Card>

      {(exitSettings?.stopLoss || exitSettings?.takeProfit || exitSettings?.timedExit) && (
        <Card className="p-4">
          <h3 className="mb-1 text-[11px] uppercase tracking-widest text-muted-foreground">
            Exits
          </h3>
          {exitSettings.stopLoss && (
            <RailRow
              label="Stop loss"
              value={formatExitConfig(exitSettings.stopLoss)}
              valueColor="var(--chart-loss)"
            />
          )}
          {exitSettings.takeProfit && (
            <RailRow
              label="Take profit"
              value={formatExitConfig(exitSettings.takeProfit)}
              valueColor="var(--chart-gain)"
            />
          )}
          {exitSettings.timedExit?.timeframe && (
            <RailRow label="Timed exit" value={formatTimeframe(exitSettings.timedExit.timeframe)} />
          )}
          {exitSettings.timedExit && (
            <RailRow
              label="Overnight"
              value={exitSettings.timedExit.avoidOvernight ? 'Avoided' : 'Allowed'}
            />
          )}
        </Card>
      )}
    </aside>
  );

  return (
    <div className="min-h-screen bg-background p-4 pt-20 text-foreground md:p-8 md:pt-8">
      <div className="mx-auto max-w-[1240px]">
        {/* ---------- Masthead ---------- */}
        <header className="mb-6 flex flex-wrap items-end gap-4 border-b-2 border-foreground/80 pb-5">
          <div>
            <div className="mb-1.5 flex items-center gap-3">
              <button
                onClick={() => navigate(getBackNavigationPath())}
                className="inline-flex items-center gap-1 text-xs uppercase tracking-widest text-muted-foreground transition-colors hover:text-foreground"
              >
                <ArrowLeft className="h-3.5 w-3.5" />
                Strategies
              </button>
              <Badge
                variant={strategy.state === 'Active' ? 'default' : 'secondary'}
                className={`rounded-full px-2.5 py-0.5 text-[11px] font-semibold uppercase tracking-wide border-transparent ${
                  strategy.state === 'Active'
                    ? 'bg-green-500/10 text-green-600 dark:text-green-400'
                    : 'bg-muted text-muted-foreground'
                }`}
              >
                <div className={`mr-1.5 h-1.5 w-1.5 rounded-full ${
                  strategy.state === 'Active' ? 'animate-pulse bg-green-600 dark:bg-green-400' : 'bg-muted-foreground'
                }`} />
                {strategy.state || 'Inactive'}
              </Badge>
              <Badge
                variant="default"
                className={`rounded-full border-transparent px-2.5 py-0.5 text-[11px] font-semibold uppercase tracking-wide ${
                  strategy.type === 'Paper'
                    ? 'bg-yellow-500/10 text-yellow-600 dark:text-yellow-400'
                    : 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400'
                }`}
              >
                {strategy.type || 'Paper'}
              </Badge>
              <Badge
                variant="default"
                className="rounded-full border border-border bg-transparent px-2.5 py-0.5 text-[11px] font-medium text-muted-foreground"
              >
                {strategy.integration || 'Default'}
              </Badge>
            </div>
            <h1 className="text-2xl font-semibold tracking-tight md:text-3xl">
              {strategy.name || 'Unnamed Strategy'}
            </h1>
            <p className="mt-1 text-[13px] text-muted-foreground tabular-nums">
              {closedCount > 0 && (
                <>
                  <b className="font-semibold text-foreground">{closedCount.toLocaleString()} closed trades</b>
                  {' '}·{' '}
                </>
              )}
              {openTrades.length} open position{openTrades.length === 1 ? '' : 's'}
            </p>
          </div>

          <div className="ml-auto flex gap-2 pb-1">
            <DropdownMenu>
              <DropdownMenuTrigger>
                <Button variant="outline" size="sm">
                  <MoreVertical className="h-4 w-4" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent className="bg-card border-border">
                <DropdownMenuItem onClick={handleCloneStrategy} className="text-foreground hover:bg-accent text-xs">
                  <Copy className="h-3 w-3 mr-2" />
                  Clone
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>

            <Button
              variant="outline"
              size="sm"
              onClick={() => navigate(`/optimus/strategy/${strategyId}/optimize`)}
            >
              <Beaker className="mr-1.5 h-4 w-4" />
              Optimize
            </Button>

            <Button
              variant="outline"
              size="sm"
              onClick={() => navigate(`/optimus/strategy/${strategyId}/edit`)}
            >
              <Settings className="mr-1.5 h-4 w-4" />
              Edit
            </Button>

            <AlertDialog>
              <AlertDialogTrigger asChild>
                <Button variant="destructive" size="sm">
                  <Trash2 className="mr-1.5 h-3 w-3" />
                  Delete
                </Button>
              </AlertDialogTrigger>
              <AlertDialogContent className="bg-card border border-border/80">
                <AlertDialogHeader>
                  <AlertDialogTitle className="text-destructive dark:text-red-400">Delete strategy</AlertDialogTitle>
                  <AlertDialogDescription className="text-muted-foreground text-sm">
                    This will permanently delete &quot;{strategy.name}&quot; and all associated data. This action cannot be undone.
                  </AlertDialogDescription>
                </AlertDialogHeader>
                <AlertDialogFooter>
                  <AlertDialogCancel className="text-xs">Cancel</AlertDialogCancel>
                  <AlertDialogAction
                    onClick={handleDeleteStrategy}
                    className="bg-destructive hover:bg-destructive/90 text-destructive-foreground text-xs"
                  >
                    Delete
                  </AlertDialogAction>
                </AlertDialogFooter>
              </AlertDialogContent>
            </AlertDialog>
          </div>
        </header>

        {/* ---------- Live state ---------- */}
        <div className="mb-6 overflow-hidden rounded-xl border border-border/80 bg-card">
          <div className="grid grid-cols-1 lg:grid-cols-12">
            <div className="border-border lg:col-span-8 lg:border-r">
              <BalanceHistoryChart
                strategyId={strategyId!}
                startingBalance={startingBalance}
                compact={true}
              />
            </div>
            <div className="lg:col-span-4">
              <StrategyStatePanel
                strategyId={strategyId!}
                startingBalance={startingBalance}
                compact={true}
              />
            </div>
          </div>
        </div>

        {/* ---------- Open positions ---------- */}
        {openTrades.length > 0 && (
          <Card className="mb-6 p-4 md:p-5">
            <h2 className="mb-2 text-sm font-semibold">Open positions</h2>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[560px] border-collapse text-[13px] tabular-nums">
                <thead>
                  <tr>
                    {['Ticker', 'Opened', 'Entry', 'Shares', 'Cost'].map((h, i) => (
                      <th
                        key={h}
                        className={`whitespace-nowrap border-b border-border px-2.5 pb-1.5 pt-2 text-[10.5px] font-semibold uppercase tracking-wider text-muted-foreground ${
                          i >= 2 ? 'text-right' : 'text-left'
                        }`}
                      >
                        {h}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {openTrades.map((trade) => (
                    <tr
                      key={trade.id}
                      onClick={() => navigate(`/optimus/trade/${trade.id}`, { state: { trade } })}
                      className="cursor-pointer border-b border-border/60 hover:bg-muted/30"
                    >
                      <td className="whitespace-nowrap px-2.5 py-1.5 font-mono font-semibold">{trade.ticker}</td>
                      <td className="whitespace-nowrap px-2.5 py-1.5 text-xs text-muted-foreground">
                        {formatOpenedAt(trade.openedAt)} ET
                      </td>
                      <td className="whitespace-nowrap px-2.5 py-1.5 text-right">${trade.entryPrice.toFixed(2)}</td>
                      <td className="whitespace-nowrap px-2.5 py-1.5 text-right">{trade.shares.toLocaleString()}</td>
                      <td className="whitespace-nowrap px-2.5 py-1.5 text-right">{formatCurrency(trade.entryPosition)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>
        )}

        {/* ---------- Performance report (shared with backtests) ---------- */}
        {tradesError ? (
          <Card className="border-destructive/30 bg-destructive/10 p-6">
            <div className="text-sm text-destructive">Failed to load trades for this strategy</div>
          </Card>
        ) : tradingData ? (
          <BacktestReport
            tradingData={tradingData}
            startingBalance={startingBalance}
            benchmarkBars={benchmarkBars}
            configRail={configRail}
          />
        ) : (
          <Card className="space-y-2 p-12 text-center">
            <h3 className="text-sm font-semibold">No closed trades yet</h3>
            <p className="text-sm text-muted-foreground">
              Performance charts and statistics will appear here once the strategy closes its first
              position.
            </p>
          </Card>
        )}
      </div>
    </div>
  );
};

export default StrategyDetailPage;
