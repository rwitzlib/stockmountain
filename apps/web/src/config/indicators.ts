import { IndicatorDefinition, IndicatorParamMeta, IndicatorSetup, IndicatorType } from '../types/tools';

const DEFAULT_COLORS = [
  '#3b82f6', // blue
  '#ef4444', // red
  '#10b981', // green
  '#f59e0b', // yellow
  '#8b5cf6', // purple
  '#06b6d4', // cyan
  '#f97316', // orange
  '#84cc16', // lime
  '#ec4899', // pink
  '#6b7280', // gray
];

export const getDefaultColor = (index: number): string => DEFAULT_COLORS[index % DEFAULT_COLORS.length];

const makeNumberParam = (key: string, label: string, defaultValue: string): IndicatorParamMeta => ({
  key,
  label,
  type: 'number',
  default: defaultValue,
});

const REGISTRY: Record<IndicatorType, IndicatorDefinition> = {
  sma: {
    type: 'sma',
    name: 'Simple Moving Average',
    category: 'trend',
    defaultPane: 0,
    defaultColor: '#3b82f6',
    params: [makeNumberParam('period', 'Period', '20')],
  },
  ema: {
    type: 'ema',
    name: 'Exponential Moving Average',
    category: 'trend',
    defaultPane: 0,
    defaultColor: '#ef4444',
    params: [makeNumberParam('period', 'Period', '20')],
  },
  macd: {
    type: 'macd',
    name: 'MACD',
    category: 'momentum',
    defaultPane: 1,
    defaultColor: '#10b981',
    // Defaults for multi-series rendering
    // macd: main line, signal: signal line, histogram: bars/line
    // Consumers can override via IndicatorSetup.colors
    defaultColors: {
      macd: '#10b981',
      signal: '#ef4444',
      histogramUp: '#10b981',
      histogramDown: '#ef4444',
    } as any,
    params: [
      makeNumberParam('fastPeriod', 'Fast Period', '12'),
      makeNumberParam('slowPeriod', 'Slow Period', '26'),
      makeNumberParam('signalPeriod', 'Signal Period', '9'),
      { key: 'source', label: 'Source', type: 'select', options: ['close', 'ema'], default: 'ema' },
    ],
  },
  rsi: {
    type: 'rsi',
    name: 'Relative Strength Index',
    category: 'momentum',
    defaultPane: 1,
    defaultColor: '#10b981',
    defaultColors: {
      rsi: '#10b981',
      upper: '#f59e0b',
      lower: '#f97316',
    } as any,
    params: [
      makeNumberParam('period', 'Period', '14'),
      makeNumberParam('overbought', 'Overbought', '70'),
      makeNumberParam('oversold', 'Oversold', '30'),
      { key: 'source', label: 'Source', type: 'select', options: ['sma', 'ema'], default: 'ema' },
    ],
  },
};

export const INDICATOR_REGISTRY = REGISTRY;

export function getIndicatorDefinition(type: IndicatorType): IndicatorDefinition {
  return REGISTRY[type];
}

export function createDefaultSetup(type: IndicatorType, indexForColor?: number): IndicatorSetup {
  const def = getIndicatorDefinition(type);
  const params = Object.fromEntries(def.params.map(p => [p.key, p.default]));
  const color = indexForColor !== undefined ? getDefaultColor(indexForColor) : def.defaultColor;
  return {
    type: def.type,
    pane: def.defaultPane,
    params,
    color,
  };
}

export const INDICATOR_TYPES: IndicatorType[] = ['sma', 'ema', 'macd', 'rsi'];


