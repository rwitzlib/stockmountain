
export interface ProfitData {
  ticker: string;
  hold: number;
  high: number;
  other?: number;
  boughtAt: string;
}

export interface ProfitStats {
  hold: StrategyStats;
  high: StrategyStats;
  other?: StrategyStats;
}

export interface StrategyStats {
  sum: number;
  average: number;
  count: number;
  winRate: number;
}
