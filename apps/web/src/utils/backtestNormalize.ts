import {
  TradingData,
  TradeStrategy,
  StrategyPortfolio,
  ExecutedTrade,
  EquityPoint,
} from '../types/types';

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

export function normalizeTradingData(raw: unknown): TradingData | null {
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
