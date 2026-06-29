export type FilterFunctionName =
  | 'sma'
  | 'ema'
  | 'macd'
  | 'crosses_over'
  | 'crosses_under'
  | 'adv'
  | 'between';

export type DraftArg =
  | string
  | null
  | {
      kind: 'draft';
      name: FilterFunctionName;
      args: DraftArg[];
    };

export type BuilderToken =
  | { type: 'literal'; value: string }
  | { type: 'func'; name: string; args: string[] }
  | { type: 'op'; value: '>' | '<' | '>=' | '<=' | '=' | '!=' }
  | { type: 'logic'; value: 'AND' | 'OR' }
  | { type: 'range'; timeframe?: string; candles?: string };

export interface FilterPaletteButton {
  label: string;
  token: BuilderToken;
  className?: string;
}

export interface FilterPaletteGroup {
  label: string;
  buttons: FilterPaletteButton[];
}

export interface FilterItem {
  id: string;
  enabled: boolean;
  expression: string;
}

export interface FilterHistoryEntry<RequestShape = unknown> {
  id: string;
  timestamp: number;
  filters: string[];
  request: RequestShape;
  resultSummary?: string;
}

export interface ChartFilterMatchRequest {
  symbol: string;
  interval: string;
  multiplier?: number;
  timespan?: 'minute' | 'hour' | 'day' | 'week' | 'year';
  range?: {
    from: string;
    to: string;
  };
  filters: string[];
  indicators?: string[];
}

export interface ChartFilterMatchResponse {
  matches: Array<number | string>;
  totalMatches?: number;
  metadata?: Record<string, unknown>;
}

