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
  Type: string;
  Value: number;
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

/** Normalized view of request settings used by list/detail UI */
export interface BacktestRequestInfo {
  positionInfo: {
    startingBalance: number;
    maxConcurrentPositions: number;
    positionSize: number;
    allowSimultaneous?: boolean;
    modelType?: string;
  };
  exitInfo?: {
    stopLoss?: {
      type?: string;
      value?: number;
    };
    profitTarget?: {
      type?: string;
      value?: number;
    };
    timedExit?: {
      timeframe: {
        multiplier: number;
        timespan?: string;
      };
    };
    timeframe?: TimeFrame;
    other?: ScanArgument;
  };
  entryInfo?: {
    filters: string[];
  };
  argument?: ScanArgument;
  filters?: string[];
}

export interface BacktestEntry {
  id: string;
  status: string;
  createdAt: string;
  creditsUsed: number;
  holdProfit: number;
  highProfit: number;
  conditionalProfit?: number;
  start: string;
  end: string;
  durationSeconds: number;
  /** Current API shape from list/get entry */
  request?: {
    start?: string;
    end?: string;
    positionSettings?: {
      startingBalance?: number;
      maxConcurrentPositions?: number;
      allowSimultaneous?: boolean;
      model?: {
        type?: string;
        size?: number;
      };
      cooldown?: {
        multiplier?: number;
        timespan?: string;
      };
    };
    exitSettings?: {
      stopLoss?: {
        type?: string;
        value?: number;
      };
      takeProfit?: {
        type?: string;
        value?: number;
      };
      timedExit?: {
        timeframe?: {
          multiplier?: number;
          timespan?: string;
        };
        avoidOvernight?: boolean;
      };
    };
    entrySettings?: {
      filters?: string[];
    };
    id?: string;
  };
  /** Legacy shape — prefer `request` when present */
  requestDetails?: {
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
      timedExit?: {
        timeframe: {
          multiplier: number;
          timespan?: string;
        };
      };
    };
    entryInfo?: {
      filters: string[];
    };
    argument?: ScanArgument;
  };
  holdStats?: BacktestEntryStatsSummary;
  highStats?: BacktestEntryStatsSummary;
  errors?: string[];
}