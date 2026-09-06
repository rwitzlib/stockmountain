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

// ---- /filters/validate + /filters/functions contracts (plans 13, 20) ----

export interface FilterTimeframe {
  multiplier: number;
  timespan: string;
}

export type FilterSegmentRole = 'function' | 'data' | 'literal' | 'op' | 'logic' | 'timeframe';

/** What a quick edit of a segment changes. */
export type FilterSegmentEdit = 'value' | 'op' | 'timeframe' | 'candles' | 'mode';

/**
 * One span of the canonical expression. Text between segments is punctuation. The client renders
 * slices of `canonical` and edits by splicing a span, then re-validates; it never rebuilds the
 * expression itself.
 */
export interface FilterSegment {
  role: FilterSegmentRole;
  /** Inclusive start offset into the canonical text. */
  start: number;
  /** Exclusive end offset into the canonical text. */
  end: number;
  edit?: FilterSegmentEdit;
}

export interface FilterValidationResult {
  expression: string;
  valid: boolean;
  error?: string;
  description?: string;
  /** Canonical spelling: explicit timeframe/mode, normalized spacing. Stored and displayed in place of the input. */
  canonical?: string;
  timeframe?: FilterTimeframe;
  segments?: FilterSegment[];
}

export interface FilterFunctionInfo {
  kind: 'function' | 'literal' | 'suffix';
  name: string;
  signature: string;
  snippet: string;
  description: string;
  /** Ordered parameter names; "?" suffix marks optional (e.g. ["series", "period?"]). */
  params?: string[];
  /** Fixed choices per parameter (name without "?"), e.g. the suffix's timeframe and mode slots. */
  paramOptions?: Record<string, string[]>;
  fields?: string[];
  /** series | transform | boolean | keyword (registry kind). */
  functionKind?: string;
  aliases?: string[];
  /** Contexts the token is valid in: "scan" | "backtest" | "chart". */
  contexts?: string[];
  /** Relative path to the user docs page, e.g. "/docs/filters/rsi". */
  docsUrl?: string;
}
