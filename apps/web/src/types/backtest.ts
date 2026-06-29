import { ScanArgument, StopConfig, TimeFrame } from "./strategy";

export interface BacktestModelSettings {
  Type: string;
  Size: number;
}

export interface BacktestCooldownSettings {
  Multiplier: number;
  Timespan: string;
}

export interface BacktestPositionSettings {
  StartingBalance: number;
  AllowSimultaneous: boolean;
  MaxConcurrentPositions: number;
  Model: BacktestModelSettings;
  Cooldown?: BacktestCooldownSettings;
}

export interface BacktestExitTarget {
  CandleType: string;
  Type: string;
  Value: number;
  PriceActionType: string;
}

export interface BacktestTimedExitSettings {
  Timeframe: {
    Multiplier: number;
    Timespan: string;
  };
}

export interface BacktestEntrySettings {
  Filters: string[];
}

export interface BacktestExitSettings {
  TakeProfit?: BacktestExitTarget;
  StopLoss?: BacktestExitTarget;
  TimedExit?: BacktestTimedExitSettings;
}

export interface BacktestRequest {
  start: string;
  end: string;
  PositionSettings: BacktestPositionSettings;
  EntrySettings: BacktestEntrySettings;
  ExitSettings: BacktestExitSettings;
}

export interface BacktestEntryStatsSummary {
  winRatio: number;
  profitFactor: number;
  totalTradesTaken: number;
  maxDrawdown: number;
  sharpeRatio: number;
}

export interface BacktestEntry {
  id: string;
  status: string;
  createdAt: string;
  creditsUsed: number;
  holdProfit: number;
  highProfit: number;
  start: string;
  end: string;
  durationSeconds: number;
  requestDetails: {
    positionInfo: {
      startingBalance: number;
      maxConcurrentPositions: number;
      positionSize: number;
    };
    exitInfo: {
      stopLoss?: StopConfig;
      profitTarget?: StopConfig;
      other?: ScanArgument;
      timeframe?: TimeFrame;
    };
    argument?: ScanArgument;
  };
  holdStats?: BacktestEntryStatsSummary;
  highStats?: BacktestEntryStatsSummary;
}