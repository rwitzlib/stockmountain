import { ReactNode, useMemo } from 'react';
import { Card } from '../ui/card';
import { EquityCurveCard, EquitySeriesDef } from './charts/EquityCurveCard';
import { DailyPnlChart } from './charts/DailyPnlChart';
import { HistogramChart } from './charts/HistogramChart';
import { EntryTimingPanel } from './EntryTimingPanel';
import { ExitReasonPanel } from './ExitReasonPanel';
import { TickerLeadersPanel } from './TickerLeadersPanel';
import { BacktestTradesTable } from './BacktestTradesTable';
import { TradingData } from '../../types/types';
import {
  formatAxisCurrency,
  formatCurrency,
  formatSignedCurrency,
  formatSignedPercent,
} from '../../utils/formatters';
import {
  computeAverageExitEfficiency,
  computeDerivedTradeStats,
  computeDrawdown,
  computeDurationHistogram,
  computeEntryTimeBuckets,
  computeExitReasonBreakdown,
  computeProfitHistogram,
  computeTickerAggregates,
} from '../../utils/backtestAnalytics';

/** Daily benchmark close keyed by YYYY-MM-DD, sorted ascending. */
export interface BenchmarkBar {
  day: string;
  close: number;
}

export function KpiTile({ label, value, sub, valueColor }: {
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

export function RailRow({ label, value, valueColor }: { label: string; value: string; valueColor?: string }) {
  return (
    <div className="flex items-baseline justify-between border-b border-border/60 py-1.5 text-[13px] last:border-b-0">
      <span className="text-muted-foreground">{label}</span>
      <span className="font-semibold tabular-nums" style={{ color: valueColor }}>{value}</span>
    </div>
  );
}

interface BacktestReportProps {
  tradingData: TradingData;
  startingBalance: number;
  /** Benchmark daily closes; when absent the benchmark series and vs-chip are omitted. */
  benchmarkBars?: BenchmarkBar[] | null;
  benchmarkLabel?: string;
  /** Right-hand config rail beside the equity curve (owner config, or masked teasers). */
  configRail?: ReactNode;
}

/**
 * The full backtest report body (verdict, KPIs, equity curve, insight panels, trades),
 * shared between the authed detail page and the public shared-backtest page. Everything
 * renders from props — no data fetching here.
 */
export function BacktestReport({
  tradingData,
  startingBalance,
  benchmarkBars,
  benchmarkLabel = 'SPY',
  configRail,
}: BacktestReportProps) {
  const analytics = useMemo(() => {
    const primary = tradingData.hold;
    const ceiling = tradingData.high;
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
  }, [tradingData, startingBalance]);

  const benchmarkSeries = useMemo(() => {
    if (!benchmarkBars?.length) return null;

    const bars = [...benchmarkBars].sort((a, b) => (a.day < b.day ? -1 : 1));
    const firstClose = bars[0].close;
    if (!firstClose) return null;

    const balances = analytics.primary.equity.map((pt) => {
      const day = pt.date.slice(0, 10);
      let close: number | null = null;
      for (const bar of bars) {
        if (bar.day <= day) {
          close = bar.close;
        } else {
          break;
        }
      }
      return close != null ? startingBalance * (close / firstClose) : null;
    });

    const lastClose = bars[bars.length - 1].close;
    return { balances, pct: (lastClose / firstClose - 1) * 100 };
  }, [analytics, benchmarkBars, startingBalance]);

  const stats = analytics.primary.stats;
  const derived = analytics.derived;
  const totalTrades = stats.totalTradesTaken ?? analytics.primary.trades.length ?? 0;

  const equitySeries: EquitySeriesDef[] = [
    {
      key: 'strategy',
      name: 'Strategy',
      color: 'var(--chart-strategy)',
      area: true,
      balances: analytics.primary.equity.map((pt) => pt.totalBalance),
    },
    ...(benchmarkSeries
      ? [
          {
            key: 'benchmark',
            name: benchmarkLabel,
            color: 'var(--chart-benchmark)',
            dashed: true,
            balances: benchmarkSeries.balances,
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
  ];

  return (
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
            {benchmarkSeries && (
              <span className="rounded-full bg-muted px-2.5 py-1 text-muted-foreground">
                vs {benchmarkLabel}{' '}
                <b
                  className="font-semibold"
                  style={{
                    color:
                      analytics.netPct >= benchmarkSeries.pct
                        ? 'var(--chart-gain)'
                        : 'var(--chart-loss)',
                  }}
                >
                  {formatSignedPercent(analytics.netPct - benchmarkSeries.pct, 1).replace('%', '')}pts
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
      <div
        className={
          configRail
            ? 'mb-6 grid grid-cols-1 items-start gap-4 lg:grid-cols-[minmax(0,1fr)_300px]'
            : 'mb-6'
        }
      >
        <EquityCurveCard
          dates={analytics.primary.equity.map((pt) => pt.date.slice(0, 10))}
          series={equitySeries}
          startingBalance={startingBalance}
          equity={analytics.primary.equity}
          drawdown={analytics.drawdown}
          footnote="Max potential assumes every position is sold at its in-trade high — an upper bound on what better exits could capture, not a peer strategy."
        />

        {configRail}
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
  );
}
