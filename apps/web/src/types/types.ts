
export interface TradeStrategy {
  endBalance: number;
  balanceChange: number;
  sumProfit: number;
  winRatio: number;
  avgWin: number;
  avgLoss: number;
  maxConcurrentPositions: number;
  totalTradesTaken?: number;
  averageDailyReturn?: number;
  dailyReturnStdDev?: number;
  sharpeRatio?: number;
  maxDrawdown?: number;
  profitFactor?: number;
}

export interface DailyResult {
  date: string;
  hold: DailyStrategyResult;
  high: DailyStrategyResult;
  other?: DailyStrategyResult;
}

interface DailyStrategyResult {
  startCashAvailable: number;
  endCashAvailable: number;
  totalBalance: number;
  profit: number;
  openPositions: number;
  bought: Trade[];
  sold: Trade[];
}

export interface Trade {
  ticker: string;
  price: number;
  shares: number;
  position: number;
  profit: number;
  timestamp: string;
  stoppedOut: boolean;
}

export interface TradingData {
  id: string;
  creditsUsed: number;
  hold: TradeStrategy;
  high: TradeStrategy;
  other?: TradeStrategy;
  results: DailyResult[];
  entries: TradingEntry[];
}

export interface TradingEntry {
  entryId: string;
  date: string;
  creditsUsed: number;
  hold: TradeStrategy;
  high: TradeStrategy;
  other?: TradeStrategy;
  results: TradeResult[];
}

export interface TradeResult {
  ticker: string;
  boughtAt: string;
  startPrice: number;
  shares: number;
  startPosition: number;
  hold: TradeOutcome;
  high: TradeOutcome;
  other?: TradeOutcome;
}

interface TradeOutcome {
  soldAt: string;
  endPrice: number;
  endPosition: number;
  profit: number;
  stoppedOut: boolean;
}
