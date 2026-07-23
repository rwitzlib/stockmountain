import { Trade } from '../types/trade';
import {
  EquityPoint,
  ExecutedTrade,
  ExitReason,
  StrategyPortfolio,
  TradeStrategy,
  TradingData,
} from '../types/types';

const KNOWN_EXIT_REASONS: ExitReason[] = [
  'timedExit',
  'takeProfit',
  'stopLoss',
  'endOfData',
  'soldAtHigh',
  'manual',
];

const EXCHANGE_TZ = 'America/New_York';

/**
 * Renders a live trade timestamp as an exchange-time ISO string
 * ("2026-07-22T09:31:00-04:00"), the format backtest results use. The analytics
 * layer (entry-time buckets, trades table) parses hours straight out of the
 * string, so the offset conversion is what keeps live trades bucketed in market
 * time rather than the server's or viewer's timezone.
 */
function toExchangeIso(timestamp: string): string {
  const date = new Date(timestamp);
  if (Number.isNaN(date.getTime())) return timestamp;

  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: EXCHANGE_TZ,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
    timeZoneName: 'longOffset',
  }).formatToParts(date);

  const get = (type: string) => parts.find((p) => p.type === type)?.value ?? '';
  // "GMT-04:00" -> "-04:00"; "GMT" (never for New York) would yield "+00:00"
  const offset = get('timeZoneName').replace('GMT', '') || '+00:00';
  // en-CA hour "24" can appear for midnight in some engines; normalize to 00
  const hour = get('hour') === '24' ? '00' : get('hour');

  return `${get('year')}-${get('month')}-${get('day')}T${hour}:${get('minute')}:${get('second')}${offset}`;
}

function toExecutedTrade(trade: Trade): ExecutedTrade {
  const exitReason = KNOWN_EXIT_REASONS.includes(trade.exitReason as ExitReason)
    ? (trade.exitReason as ExitReason)
    : undefined;

  return {
    ticker: trade.ticker,
    boughtAt: toExchangeIso(trade.openedAt),
    soldAt: toExchangeIso(trade.closedAt),
    startPrice: trade.entryPrice,
    endPrice: trade.closePrice,
    shares: trade.shares,
    startPosition: trade.entryPosition,
    endPosition: trade.closePosition,
    profit: trade.profit ?? 0,
    stoppedOut: exitReason === 'stopLoss',
    exitReason,
  };
}

function sampleStdDev(values: number[]): number {
  if (values.length <= 1) return 0;
  const mean = values.reduce((a, b) => a + b, 0) / values.length;
  const sumSq = values.reduce((acc, v) => acc + (v - mean) ** 2, 0);
  return Math.sqrt(sumSq / (values.length - 1));
}

/** Same peak-to-trough fraction the backtester's portfolio simulator reports. */
function maxDrawdownFraction(balances: number[]): number {
  let peak = balances.length ? balances[0] : 0;
  let maxDd = 0;
  for (const balance of balances) {
    peak = Math.max(peak, balance);
    if (peak > 0) maxDd = Math.max(maxDd, (peak - balance) / peak);
  }
  return maxDd;
}

const EMPTY_STATS: TradeStrategy = {
  endBalance: 0,
  balanceChange: 0,
  sumProfit: 0,
  winRatio: 0,
  avgWin: 0,
  avgLoss: 0,
  maxConcurrentPositions: 0,
  totalTradesTaken: 0,
};

const EMPTY_PORTFOLIO: StrategyPortfolio = { stats: EMPTY_STATS, equity: [], trades: [] };

/**
 * Reshapes a live strategy's closed trades into the backtest result contract so
 * BacktestReport renders live performance with the exact same KPIs, charts, and
 * trades table as a backtest. Stats formulas mirror BacktestPortfolioSimulator
 * (sample-stddev daily returns, sqrt(252) Sharpe, peak-to-trough drawdown
 * fraction, gross-win/gross-loss profit factor). The `high` (max potential)
 * portfolio has no live equivalent and is returned empty.
 *
 * Returns null when there are no closed trades to report on.
 */
export function buildLiveTradingData(
  trades: Trade[],
  startingBalance: number
): Pick<TradingData, 'hold' | 'high'> | null {
  const closed = trades
    .filter((t) => t.closedAt)
    .map(toExecutedTrade)
    .sort((a, b) => new Date(a.soldAt).getTime() - new Date(b.soldAt).getTime());

  if (closed.length === 0 || !(startingBalance > 0)) return null;

  // ---- Equity curve: one point per exchange-time day with at least one exit ----
  const byDay = new Map<string, ExecutedTrade[]>();
  for (const t of closed) {
    const day = t.soldAt.slice(0, 10);
    let bucket = byDay.get(day);
    if (!bucket) {
      bucket = [];
      byDay.set(day, bucket);
    }
    bucket.push(t);
  }

  const intervals = closed.map((t) => ({
    open: new Date(t.boughtAt).getTime(),
    close: new Date(t.soldAt).getTime(),
  }));

  /** Max positions simultaneously open in [from, to], via an open/close event sweep. */
  const maxConcurrentIn = (from: number, to: number): number => {
    const events: { ts: number; delta: number }[] = [];
    for (const iv of intervals) {
      if (iv.open > to || iv.close < from) continue;
      events.push({ ts: Math.max(iv.open, from), delta: 1 });
      events.push({ ts: iv.close, delta: -1 });
    }
    events.sort((a, b) => a.ts - b.ts || a.delta - b.delta);
    let cur = 0;
    let max = 0;
    for (const e of events) {
      cur += e.delta;
      max = Math.max(max, cur);
    }
    return max;
  };

  const equity: EquityPoint[] = [];
  let balance = startingBalance;
  for (const [day, dayTrades] of [...byDay.entries()].sort(([a], [b]) => (a < b ? -1 : 1))) {
    const dayProfit = dayTrades.reduce((sum, t) => sum + t.profit, 0);
    const startBalance = balance;
    balance += dayProfit;

    const dayStart = new Date(`${day}T00:00:00`).getTime();
    const dayEnd = new Date(`${day}T23:59:59`).getTime();

    equity.push({
      date: day,
      startCash: startBalance,
      endCash: balance,
      totalBalance: balance,
      openPositions: intervals.filter((iv) => iv.open <= dayEnd && iv.close > dayEnd).length,
      maxConcurrentPositions: maxConcurrentIn(dayStart, dayEnd),
      dayProfit,
      tradesTaken: dayTrades.length,
    });
  }

  // ---- Stats, matching the backtester's formulas ----
  const wins = closed.filter((t) => t.profit > 0);
  const losses = closed.filter((t) => t.profit < 0);
  const sumProfit = closed.reduce((sum, t) => sum + t.profit, 0);
  const grossWin = wins.reduce((sum, t) => sum + t.profit, 0);
  const grossLoss = losses.reduce((sum, t) => sum + Math.abs(t.profit), 0);

  const dailyReturns: number[] = [];
  for (let i = 0; i < equity.length; i++) {
    const previous = i === 0 ? startingBalance : equity[i - 1].totalBalance;
    if (previous > 0) dailyReturns.push(equity[i].totalBalance / previous - 1);
  }
  const averageDailyReturn = dailyReturns.length
    ? dailyReturns.reduce((a, b) => a + b, 0) / dailyReturns.length
    : 0;
  const dailyReturnStdDev = sampleStdDev(dailyReturns);

  const stats: TradeStrategy = {
    endBalance: startingBalance + sumProfit,
    balanceChange: sumProfit,
    sumProfit,
    winRatio: closed.length ? wins.length / closed.length : 0,
    avgWin: wins.length ? grossWin / wins.length : 0,
    avgLoss: losses.length ? grossLoss / losses.length : 0,
    maxConcurrentPositions: maxConcurrentIn(-Infinity, Infinity),
    totalTradesTaken: closed.length,
    averageDailyReturn,
    dailyReturnStdDev,
    sharpeRatio: dailyReturnStdDev > 0 ? (Math.sqrt(252) * averageDailyReturn) / dailyReturnStdDev : 0,
    maxDrawdown: maxDrawdownFraction([startingBalance, ...equity.map((pt) => pt.totalBalance)]),
    profitFactor: grossLoss > 0 ? grossWin / grossLoss : grossWin > 0 ? Infinity : 0,
  };

  return {
    hold: { stats, equity, trades: closed },
    high: EMPTY_PORTFOLIO,
  };
}
