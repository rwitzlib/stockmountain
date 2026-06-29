export interface StockMarketData {
  ticker: string;
  queryCount: number;
  resultsCount: number;
  adjusted: boolean;
  results: BarData[];
  status: string;
  request_id: string;
  count: number;
  indicators: IndicatorConfig[];
}

export interface IndicatorConfig {
  name: string;
  results: IndicatorPoint[];
}

export interface IndicatorPoint {
  timestamp: number;
  value: number;
  // Optional fields for multi-line indicators
  signal?: number;      // MACD signal line
  histogram?: number;   // MACD histogram
  upper?: number;       // RSI upper band
  lower?: number;       // RSI lower band
}

export interface BarData {
  v: number;    // volume
  vw: number;   // volume weighted average price
  o: number;    // open
  c: number;    // close
  h: number;    // high
  l: number;    // low
  t: number;    // timestamp
  n: number;    // number of trades
}

export type IndicatorType = 'sma' | 'ema' | 'macd' | 'rsi';

export interface IndicatorConfig {
  name: string;
  results: IndicatorPoint[];
}

export interface IndicatorSetup {
  type: IndicatorType;
  pane: number; // Pane number for rendering (0 = main chart, 1+ = separate panes)
  params: Record<string, number | string>;
  color?: string; // Hex color code for the indicator line
  colors?: Record<string, string>; // Optional colors per series key, e.g., { macd, signal, histogram, histogramUp, histogramDown }
}

// Indicator metadata for UI/registry
export type IndicatorCategory = 'trend' | 'momentum' | 'volume' | 'volatility' | 'other';

export interface IndicatorParamMeta {
  key: string;
  label: string;
  type: 'number' | 'select';
  default: string;
  options?: string[];
}

export interface IndicatorDefinition {
  type: IndicatorType;
  name: string;
  category: IndicatorCategory;
  defaultPane: number;
  defaultColor: string;
  defaultColors?: Record<string, string>;
  params: IndicatorParamMeta[];
}

export interface IndicatorsRequest {
  ticker: string;
  Multiplier: number;
  Timespan: 'minute' | 'hour' | 'day' | 'week' | 'year';
  From: string;
  To: string;
  limit: number;
  Indicators: string[];
}