import { EquityPoint, ExecutedTrade } from '../types/types';

export interface HistogramBin {
  x0: number;
  count: number;
}

export interface Histogram {
  bins: HistogramBin[];
  binSize: number;
}

export interface EntryTimeBucket {
  label: string;
  pnl: number;
  count: number;
  winRate: number;
}

export interface TickerAggregate {
  ticker: string;
  pnl: number;
  trades: number;
  wins: number;
}

export interface DerivedTradeStats {
  expectancy: number;
  wins: number;
  losses: number;
  medianHoldMinutes: number;
  /** Share (0-1) of trades that held until the longest observed duration, i.e. rode to the timed exit */
  fullHoldPct: number;
  maxHoldMinutes: number;
  winStreak: number;
  lossStreak: number;
  uniqueTickers: number;
  bestTrade: number;
  worstTrade: number;
}

export function tradeDurationMinutes(trade: ExecutedTrade): number {
  const bought = new Date(trade.boughtAt).getTime();
  const sold = new Date(trade.soldAt).getTime();
  if (!Number.isFinite(bought) || !Number.isFinite(sold) || sold < bought) return 0;
  return (sold - bought) / 60000;
}

export function tradeReturnPct(trade: ExecutedTrade): number {
  return trade.startPosition ? (trade.profit / trade.startPosition) * 100 : 0;
}

/** A step size from the 1/2/2.5/5 family so axis ticks and bin edges land on friendly values. */
export function niceStep(rough: number): number {
  if (!Number.isFinite(rough) || rough <= 0) return 1;
  const mag = Math.pow(10, Math.floor(Math.log10(rough)));
  for (const m of [1, 2, 2.5, 5, 10]) {
    if (rough <= m * mag) return m * mag;
  }
  return 10 * mag;
}

export function niceTicks(min: number, max: number, targetCount: number): number[] {
  const span = max - min || 1;
  const step = niceStep(span / targetCount);
  const ticks: number[] = [];
  for (let v = Math.ceil(min / step) * step; v <= max + step * 1e-6; v += step) {
    ticks.push(Number(v.toFixed(6)));
  }
  return ticks;
}

export function computeDerivedTradeStats(trades: ExecutedTrade[]): DerivedTradeStats | null {
  if (trades.length === 0) return null;

  const chronological = [...trades].sort(
    (a, b) => new Date(a.boughtAt).getTime() - new Date(b.boughtAt).getTime()
  );

  let wins = 0;
  let losses = 0;
  let sum = 0;
  let best = -Infinity;
  let worst = Infinity;
  let winStreak = 0;
  let lossStreak = 0;
  let curWin = 0;
  let curLoss = 0;
  const tickers = new Set<string>();

  for (const t of chronological) {
    const p = t.profit ?? 0;
    sum += p;
    best = Math.max(best, p);
    worst = Math.min(worst, p);
    tickers.add(t.ticker);
    if (p > 0) {
      wins++;
      curWin++;
      curLoss = 0;
    } else if (p < 0) {
      losses++;
      curLoss++;
      curWin = 0;
    } else {
      curWin = 0;
      curLoss = 0;
    }
    winStreak = Math.max(winStreak, curWin);
    lossStreak = Math.max(lossStreak, curLoss);
  }

  const durations = trades.map(tradeDurationMinutes).sort((a, b) => a - b);
  const medianHoldMinutes = durations[Math.floor(durations.length / 2)];
  const maxHoldMinutes = durations[durations.length - 1];
  const fullHoldCount = durations.filter((d) => d >= maxHoldMinutes - 0.5).length;

  return {
    expectancy: sum / trades.length,
    wins,
    losses,
    medianHoldMinutes,
    fullHoldPct: fullHoldCount / trades.length,
    maxHoldMinutes,
    winStreak,
    lossStreak,
    uniqueTickers: tickers.size,
    bestTrade: best,
    worstTrade: worst,
  };
}

/** Per-day drawdown from the running high-water mark, as negative percentages. */
export function computeDrawdown(equity: EquityPoint[]): number[] {
  let hwm = equity.length ? equity[0].startCash || equity[0].totalBalance : 0;
  return equity.map((pt) => {
    hwm = Math.max(hwm, pt.totalBalance);
    return hwm > 0 ? (pt.totalBalance / hwm - 1) * 100 : 0;
  });
}

/**
 * Histogram of per-trade P&L. Bin edges land on nice dollar values; the 1st/99th
 * percentiles bound the range so a single outlier can't flatten the shape —
 * outliers are clamped into the edge bins.
 */
export function computeProfitHistogram(trades: ExecutedTrade[], targetBins = 24): Histogram {
  if (trades.length === 0) return { bins: [], binSize: 1 };
  const profits = trades.map((t) => t.profit ?? 0).sort((a, b) => a - b);
  const lo = profits[Math.floor(profits.length * 0.01)];
  const hi = profits[Math.min(profits.length - 1, Math.floor(profits.length * 0.99))];
  const binSize = niceStep(Math.max(hi - lo, 1) / targetBins);
  const x0 = Math.floor(lo / binSize) * binSize;
  const x1 = Math.ceil((hi + binSize * 0.001) / binSize) * binSize;
  const n = Math.max(1, Math.round((x1 - x0) / binSize));

  const counts = new Array(n).fill(0);
  for (const p of profits) {
    const i = Math.min(n - 1, Math.max(0, Math.floor((p - x0) / binSize)));
    counts[i]++;
  }
  return {
    bins: counts.map((count, i) => ({ x0: x0 + i * binSize, count })),
    binSize,
  };
}

/** Histogram of time-in-trade in minutes. */
export function computeDurationHistogram(trades: ExecutedTrade[], targetBins = 15): Histogram {
  if (trades.length === 0) return { bins: [], binSize: 1 };
  const durations = trades.map(tradeDurationMinutes);
  const max = Math.max(...durations, 1);
  const binSize = Math.max(1, niceStep(max / targetBins));
  const n = Math.floor(max / binSize) + 1;

  const counts = new Array(n).fill(0);
  for (const d of durations) {
    counts[Math.min(n - 1, Math.floor(d / binSize))]++;
  }
  return {
    bins: counts.map((count, i) => ({ x0: i * binSize, count })),
    binSize,
  };
}

/**
 * P&L bucketed by entry time-of-day in *exchange* time, parsed straight from the
 * timestamp string (e.g. "2026-06-01T09:30:00-04:00") so the viewer's local
 * timezone never shifts the buckets.
 */
export function computeEntryTimeBuckets(trades: ExecutedTrade[]): EntryTimeBucket[] {
  const defs: { label: string; test: (hhmm: string) => boolean }[] = [
    { label: 'Pre-market', test: (t) => t < '09:30' },
    { label: '9:30 open', test: (t) => t === '09:30' },
    { label: '9:31–10:00', test: (t) => t > '09:30' && t <= '10:00' },
    { label: '10:00–11:00', test: (t) => t > '10:00' && t <= '11:00' },
    { label: '11:00–14:00', test: (t) => t > '11:00' && t <= '14:00' },
    { label: 'After 14:00', test: (t) => t > '14:00' },
  ];

  const buckets = defs.map((d) => ({ label: d.label, pnl: 0, count: 0, wins: 0 }));
  for (const trade of trades) {
    const m = /T(\d{2}:\d{2})/.exec(trade.boughtAt);
    if (!m) continue;
    const idx = defs.findIndex((d) => d.test(m[1]));
    if (idx < 0) continue;
    buckets[idx].pnl += trade.profit ?? 0;
    buckets[idx].count++;
    if ((trade.profit ?? 0) > 0) buckets[idx].wins++;
  }

  return buckets
    .filter((b) => b.count > 0)
    .map((b) => ({
      label: b.label,
      pnl: b.pnl,
      count: b.count,
      winRate: b.count ? (b.wins / b.count) * 100 : 0,
    }));
}

export function computeTickerAggregates(
  trades: ExecutedTrade[],
  topN = 6
): { best: TickerAggregate[]; worst: TickerAggregate[] } {
  const byTicker = new Map<string, TickerAggregate>();
  for (const t of trades) {
    let agg = byTicker.get(t.ticker);
    if (!agg) {
      agg = { ticker: t.ticker, pnl: 0, trades: 0, wins: 0 };
      byTicker.set(t.ticker, agg);
    }
    agg.pnl += t.profit ?? 0;
    agg.trades++;
    if ((t.profit ?? 0) > 0) agg.wins++;
  }
  const sorted = [...byTicker.values()].sort((a, b) => b.pnl - a.pnl);
  return {
    best: sorted.slice(0, topN),
    worst: sorted.slice(-topN).reverse(),
  };
}
