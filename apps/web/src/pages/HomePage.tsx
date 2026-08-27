import { ReactNode, useMemo } from 'react';
import { Link } from 'react-router-dom';
import { useQueries, useQuery } from '@tanstack/react-query';
import { ArrowRight, Plus } from 'lucide-react';
import { strategyApi } from '../api/strategyApi';
import { backtestApi } from '../api/backtestApi';
import { Strategy, StrategyStateResponse, BalanceHistoryEntry } from '../types/strategy';
import { BacktestEntry } from '../types/backtest';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../components/ui/table';
import {
  formatCurrency,
  formatSignedCurrency,
  formatSignedPercent,
  formatShortDate,
} from '../utils/formatters';
import { getPercentGain } from '../utils/backtestRequest';

/** Most recent balance snapshot strictly before today — the "previous close" for daily P&L. */
function priorClose(history: BalanceHistoryEntry[] | undefined): BalanceHistoryEntry | null {
  if (!history?.length) return null;
  const today = new Date().toISOString().slice(0, 10);
  const prior = history.filter((e) => e.date.slice(0, 10) < today);
  if (!prior.length) return null;
  return prior.reduce((a, b) => (a.recordedAt > b.recordedAt ? a : b));
}

function pnlColor(value: number | null | undefined): string {
  if (value == null || value === 0) return 'text-muted-foreground';
  return value > 0 ? 'text-gain' : 'text-loss';
}

interface StatTileProps {
  label: string;
  value: ReactNode;
  hint?: string;
  loading?: boolean;
}

function StatTile({ label, value, hint, loading }: StatTileProps) {
  return (
    <div className="rounded-xl border border-border/80 bg-card p-4">
      <div className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">
        {label}
      </div>
      <div className="mt-1.5 text-2xl font-semibold tracking-tight text-foreground">
        {loading ? <span className="animate-pulse text-muted-foreground">…</span> : value}
      </div>
      {hint && <div className="mt-0.5 text-xs text-muted-foreground">{hint}</div>}
    </div>
  );
}

function Sparkline({ points }: { points: number[] }) {
  if (points.length < 2) {
    return <span className="text-xs text-muted-foreground">—</span>;
  }
  const w = 96;
  const h = 28;
  const pad = 2;
  const min = Math.min(...points);
  const max = Math.max(...points);
  const range = max - min || 1;
  const step = (w - pad * 2) / (points.length - 1);
  const d = points
    .map((p, i) => {
      const x = (pad + i * step).toFixed(1);
      const y = (h - pad - ((p - min) / range) * (h - pad * 2)).toFixed(1);
      return `${i === 0 ? 'M' : 'L'}${x},${y}`;
    })
    .join(' ');
  return (
    <svg viewBox={`0 0 ${w} ${h}`} className="h-7 w-24 text-muted-foreground" aria-hidden="true">
      <path
        d={d}
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

function SectionCard({
  title,
  action,
  children,
}: {
  title: string;
  action?: ReactNode;
  children: ReactNode;
}) {
  return (
    <section className="rounded-xl border border-border/80 bg-card">
      <div className="flex items-center justify-between border-b border-border px-4 py-3">
        <h2 className="text-sm font-semibold tracking-tight text-foreground">{title}</h2>
        {action}
      </div>
      {children}
    </section>
  );
}

function SectionLink({ to, children }: { to: string; children: ReactNode }) {
  return (
    <Link
      to={to}
      className="inline-flex items-center gap-1 text-xs font-medium text-muted-foreground transition-colors hover:text-foreground"
    >
      {children}
      <ArrowRight className="h-3.5 w-3.5" />
    </Link>
  );
}

function EmptyState({ message, cta, to }: { message: string; cta: string; to: string }) {
  return (
    <div className="px-4 py-10 text-center">
      <p className="mb-3 text-sm text-muted-foreground">{message}</p>
      <Link
        to={to}
        className="inline-flex items-center gap-2 rounded-lg border border-border px-3 py-2 text-xs font-medium text-muted-foreground transition-colors hover:bg-accent hover:text-foreground"
      >
        <Plus className="h-3.5 w-3.5" />
        {cta}
      </Link>
    </div>
  );
}

function ErrorNotice({ message }: { message: string }) {
  return <div className="px-4 py-10 text-center text-sm text-destructive">{message}</div>;
}

function backtestStatusLabel(status: string): ReactNode {
  if (status === 'InProgress') {
    return <span className="animate-pulse text-muted-foreground">Running…</span>;
  }
  if (status === 'Failed') {
    return <span className="text-loss">Failed</span>;
  }
  return <span className="text-muted-foreground">{status}</span>;
}

export function HomePage() {
  const {
    data: strategies,
    isLoading: strategiesLoading,
    isError: strategiesError,
  } = useQuery({
    queryKey: ['myStrategies'],
    queryFn: strategyApi.getMyStrategies,
  });

  const activeStrategies = useMemo(
    () => (strategies ?? []).filter((s): s is Strategy & { id: string } => s.state === 'Active' && !!s.id),
    [strategies],
  );

  const historyStart = useMemo(
    () => new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10),
    [],
  );

  const stateQueries = useQueries({
    queries: activeStrategies.map((s) => ({
      queryKey: ['strategyState', s.id],
      queryFn: () => strategyApi.getStrategyState(s.id),
      staleTime: 60_000,
    })),
  });

  const historyQueries = useQueries({
    queries: activeStrategies.map((s) => ({
      queryKey: ['balanceHistory', s.id, historyStart],
      queryFn: () => strategyApi.getBalanceHistory(s.id, historyStart),
      staleTime: 5 * 60_000,
    })),
  });

  const statesLoading = strategiesLoading || stateQueries.some((q) => q.isLoading);
  const dailyLoading = statesLoading || historyQueries.some((q) => q.isLoading);
  // Per-strategy fetch failures leave that strategy out of the totals, so the
  // aggregates are only trustworthy when every state/history query succeeded.
  const stateErrorCount = stateQueries.filter((q) => q.isError).length;
  const historyErrorCount = historyQueries.filter((q) => q.isError).length;
  const partialData = stateErrorCount > 0 || historyErrorCount > 0;

  const states = stateQueries.map((q) => q.data);
  const histories = historyQueries.map((q) => q.data);

  const overview = useMemo(() => {
    let totalBalance = 0;
    let unrealizedPnl = 0;
    let openPositions = 0;
    let dailyPnl: number | null = null;
    const rows = activeStrategies.map((strategy, i) => {
      const state: StrategyStateResponse | undefined = states[i];
      const history = histories[i]?.history;
      const prev = priorClose(history);
      const daily = state && prev ? state.currentBalance - prev.currentBalance : null;
      if (state) {
        totalBalance += state.currentBalance;
        unrealizedPnl += state.unrealizedPnl;
        openPositions += state.openPositionsCount;
      }
      if (daily != null) {
        dailyPnl = (dailyPnl ?? 0) + daily;
      }
      const spark = history
        ? [...history].sort((a, b) => a.recordedAt - b.recordedAt).map((e) => e.currentBalance)
        : [];
      return { strategy, state, daily, spark };
    });
    return { totalBalance, unrealizedPnl, openPositions, dailyPnl, rows };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeStrategies, ...states, ...histories]);

  const {
    data: backtests,
    isLoading: backtestsLoading,
    isError: backtestsError,
  } = useQuery({
    queryKey: ['backtests'],
    queryFn: backtestApi.getBacktests,
  });

  const recentBacktests = useMemo(
    () =>
      [...(backtests ?? [])]
        .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
        .slice(0, 5),
    [backtests],
  );

  const topBacktests = useMemo(
    () =>
      (backtests ?? [])
        .filter((b) => b.status === 'Completed')
        .map((b) => ({ entry: b, pct: getPercentGain(b) }))
        .filter((b): b is { entry: BacktestEntry; pct: number } => b.pct != null)
        .sort((a, b) => b.pct - a.pct)
        .slice(0, 5),
    [backtests],
  );

  const hasActive = activeStrategies.length > 0;

  return (
    <div className="min-h-screen bg-background">
      <div className="mx-auto max-w-7xl space-y-6 px-4 py-8 pt-20 md:pt-8">
        <div className="flex flex-wrap items-end justify-between gap-4 border-b border-border pb-5">
          <div>
            <div className="mb-1 text-[11px] font-medium uppercase tracking-widest text-muted-foreground">
              StockMountain
            </div>
            <h1 className="text-2xl font-semibold tracking-tight text-foreground">Dashboard</h1>
            <p className="mt-1 text-sm text-muted-foreground">
              P&L and activity across your strategies.
            </p>
          </div>
          <div className="flex items-center gap-2">
            <Link
              to="/backtest/create"
              className="inline-flex items-center gap-2 rounded-lg border border-border px-3 py-2 text-xs font-medium text-muted-foreground transition-colors hover:bg-accent hover:text-foreground"
            >
              <Plus className="h-3.5 w-3.5" />
              New backtest
            </Link>
            <Link
              to="/strategies/new"
              className="inline-flex items-center gap-2 rounded-lg bg-primary px-3 py-2 text-xs font-medium text-primary-foreground transition-colors hover:bg-primary/90"
            >
              <Plus className="h-3.5 w-3.5" />
              New strategy
            </Link>
          </div>
        </div>

        {/* Partial-failure warning — totals below exclude strategies whose data failed */}
        {partialData && (
          <div className="rounded-lg border border-destructive/40 bg-destructive/5 px-3 py-2 text-xs text-destructive">
            Some strategy data failed to load — the totals below may be incomplete.
          </div>
        )}

        {/* KPI row — totals across active strategies */}
        <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
          <StatTile
            label="Total balance"
            loading={statesLoading}
            value={hasActive && !strategiesError ? formatCurrency(overview.totalBalance) : '—'}
            hint={
              strategiesError
                ? 'failed to load'
                : `${activeStrategies.length} active ${activeStrategies.length === 1 ? 'strategy' : 'strategies'}${
                    stateErrorCount > 0 ? ` · ${stateErrorCount} unavailable` : ''
                  }`
            }
          />
          <StatTile
            label="Today's P&L"
            loading={dailyLoading}
            value={
              overview.dailyPnl != null ? (
                <span className={pnlColor(overview.dailyPnl)}>
                  {formatSignedCurrency(overview.dailyPnl)}
                </span>
              ) : (
                '—'
              )
            }
            hint={strategiesError ? 'failed to load' : 'vs previous close'}
          />
          <StatTile
            label="Unrealized P&L"
            loading={statesLoading}
            value={
              hasActive && !strategiesError ? (
                <span className={pnlColor(overview.unrealizedPnl)}>
                  {formatSignedCurrency(overview.unrealizedPnl)}
                </span>
              ) : (
                '—'
              )
            }
            hint={strategiesError ? 'failed to load' : 'open positions'}
          />
          <StatTile
            label="Open positions"
            loading={statesLoading}
            value={hasActive && !strategiesError ? overview.openPositions : '—'}
            hint={strategiesError ? 'failed to load' : undefined}
          />
        </div>

        {/* Active strategies */}
        <SectionCard
          title="Active strategies"
          action={<SectionLink to="/strategies">Manage</SectionLink>}
        >
          {strategiesLoading ? (
            <div className="px-4 py-10 text-center text-sm text-muted-foreground animate-pulse">
              Loading strategies…
            </div>
          ) : strategiesError ? (
            <ErrorNotice message="Couldn't load your strategies. Refresh the page to try again." />
          ) : !hasActive ? (
            <EmptyState
              message="No active strategies yet."
              cta="Create a strategy"
              to="/strategies/new"
            />
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">
                      Strategy
                    </TableHead>
                    <TableHead className="text-right text-[11px] font-medium uppercase tracking-widest text-muted-foreground">
                      Balance
                    </TableHead>
                    <TableHead className="text-right text-[11px] font-medium uppercase tracking-widest text-muted-foreground">
                      Today
                    </TableHead>
                    <TableHead className="text-right text-[11px] font-medium uppercase tracking-widest text-muted-foreground">
                      Unrealized
                    </TableHead>
                    <TableHead className="text-right text-[11px] font-medium uppercase tracking-widest text-muted-foreground">
                      Positions
                    </TableHead>
                    <TableHead className="text-right text-[11px] font-medium uppercase tracking-widest text-muted-foreground">
                      Last 30d
                    </TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {overview.rows.map(({ strategy, state, daily, spark }) => (
                    <TableRow key={strategy.id}>
                      <TableCell>
                        <Link
                          to={`/strategies/${strategy.id}`}
                          className="text-sm font-medium text-foreground transition-colors hover:underline"
                        >
                          {strategy.name}
                        </Link>
                        <span className="ml-2 text-[10px] uppercase tracking-wider text-muted-foreground">
                          {strategy.type}
                        </span>
                      </TableCell>
                      <TableCell className="text-right text-sm tabular-nums text-foreground">
                        {state ? formatCurrency(state.currentBalance) : '—'}
                      </TableCell>
                      <TableCell className={`text-right text-sm tabular-nums ${pnlColor(daily)}`}>
                        {daily != null ? formatSignedCurrency(daily) : '—'}
                      </TableCell>
                      <TableCell
                        className={`text-right text-sm tabular-nums ${pnlColor(state?.unrealizedPnl)}`}
                      >
                        {state ? formatSignedCurrency(state.unrealizedPnl) : '—'}
                      </TableCell>
                      <TableCell className="text-right text-sm tabular-nums text-foreground">
                        {state ? state.openPositionsCount : '—'}
                      </TableCell>
                      <TableCell className="text-right">
                        <div className="flex justify-end">
                          <Sparkline points={spark} />
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </SectionCard>

        <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
          {/* Recent backtests */}
          <SectionCard
            title="Recent backtests"
            action={<SectionLink to="/backtest">View all</SectionLink>}
          >
            {backtestsLoading ? (
              <div className="px-4 py-10 text-center text-sm text-muted-foreground animate-pulse">
                Loading backtests…
              </div>
            ) : backtestsError ? (
              <ErrorNotice message="Couldn't load backtests. Refresh the page to try again." />
            ) : recentBacktests.length === 0 ? (
              <EmptyState
                message="No backtests yet."
                cta="Run a backtest"
                to="/backtest/create"
              />
            ) : (
              <ul className="divide-y divide-border">
                {recentBacktests.map((entry) => {
                  const pct = getPercentGain(entry);
                  return (
                    <li key={entry.id}>
                      <Link
                        to={`/backtest/${entry.id}`}
                        className="flex items-center justify-between gap-4 px-4 py-3 transition-colors hover:bg-accent/40"
                      >
                        <div className="min-w-0">
                          <div className="truncate text-sm font-medium text-foreground">
                            {formatShortDate(entry.start)} – {formatShortDate(entry.end)}
                          </div>
                          <div className="text-xs text-muted-foreground">
                            Created {formatShortDate(entry.createdAt)}
                          </div>
                        </div>
                        <div className="text-right text-sm tabular-nums">
                          {entry.status === 'Completed' && pct != null ? (
                            <span className={`font-medium ${pnlColor(pct)}`}>
                              {formatSignedPercent(pct)}
                            </span>
                          ) : (
                            <span className="text-xs">{backtestStatusLabel(entry.status)}</span>
                          )}
                        </div>
                      </Link>
                    </li>
                  );
                })}
              </ul>
            )}
          </SectionCard>

          {/* Top performers */}
          <SectionCard
            title="Top backtests"
            action={<SectionLink to="/backtest">View all</SectionLink>}
          >
            {backtestsLoading ? (
              <div className="px-4 py-10 text-center text-sm text-muted-foreground animate-pulse">
                Loading backtests…
              </div>
            ) : backtestsError ? (
              <ErrorNotice message="Couldn't load backtests. Refresh the page to try again." />
            ) : topBacktests.length === 0 ? (
              <div className="px-4 py-10 text-center text-sm text-muted-foreground">
                Completed backtests will be ranked here by return.
              </div>
            ) : (
              <ul className="divide-y divide-border">
                {topBacktests.map(({ entry, pct }, i) => (
                  <li key={entry.id}>
                    <Link
                      to={`/backtest/${entry.id}`}
                      className="flex items-center justify-between gap-4 px-4 py-3 transition-colors hover:bg-accent/40"
                    >
                      <div className="flex min-w-0 items-center gap-3">
                        <span className="w-5 shrink-0 text-xs tabular-nums text-muted-foreground">
                          {i + 1}
                        </span>
                        <div className="min-w-0">
                          <div className="truncate text-sm font-medium text-foreground">
                            {formatShortDate(entry.start)} – {formatShortDate(entry.end)}
                          </div>
                          <div className="text-xs text-muted-foreground">
                            {formatSignedCurrency(entry.holdProfit || 0)}
                          </div>
                        </div>
                      </div>
                      <span className={`text-sm font-medium tabular-nums ${pnlColor(pct)}`}>
                        {formatSignedPercent(pct)}
                      </span>
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </SectionCard>
        </div>
      </div>
    </div>
  );
}
