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

// ---- /filters/validate + /filters/functions contracts (plan 13) ----

export interface FilterTimeframe {
  multiplier: number;
  timespan: string;
}

export interface FilterAstNode {
  kind: 'binary' | 'function' | 'field' | 'data' | 'literal' | 'range' | 'raw';
  op?: string;
  left?: FilterAstNode;
  right?: FilterAstNode;
  name?: string;
  args?: FilterAstNode[];
  field?: string;
  target?: FilterAstNode;
  value?: string;
  inner?: FilterAstNode;
  timeframe?: FilterTimeframe;
  candles?: number;
}

export interface FilterValidationResult {
  expression: string;
  valid: boolean;
  error?: string;
  description?: string;
  timeframe?: FilterTimeframe;
  ast?: FilterAstNode;
}

export interface FilterFunctionInfo {
  kind: 'function' | 'literal' | 'operator';
  name: string;
  signature: string;
  snippet: string;
  description: string;
  fields?: string[];
}
