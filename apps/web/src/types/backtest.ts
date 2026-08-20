import type {
  ScanArgument,
  StopConfig,
  TimeFrame,
  PositionSettings,
  ExitSettings,
  EntrySettings,
} from "./strategy";

/**
 * Backtest create payload — the backend reuses the strategy settings models, so the
 * frontend does too. Cooldown is optional (omit when disabled); stop loss, take profit,
 * and timed exit are mandatory.
 */
export interface BacktestRequest {
  start: string;
  end: string;
  positionSettings: PositionSettings;
  entrySettings: EntrySettings;
  exitSettings: ExitSettings;
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

/** Sortable table columns — entry fields plus derived values. */
export type BacktestSortKey = keyof BacktestEntry | 'percentGain';

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