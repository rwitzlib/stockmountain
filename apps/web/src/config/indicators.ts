import { IndicatorDefinition, IndicatorParamMeta, IndicatorSetup, IndicatorType } from '../types/tools';

const DEFAULT_COLORS = [
  '#14a3bd', // teal
  '#8b5cf6', // violet
  '#d97706', // amber
  '#e05252', // rose
  '#2fae60', // emerald
  '#6366f1', // slate blue
  '#b45309', // dark amber
  '#64748b', // slate
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
    defaultColor: '#14a3bd',
    params: [makeNumberParam('period', 'Period', '20')],
  },
  ema: {
    type: 'ema',
    name: 'Exponential Moving Average',
    category: 'trend',
    defaultPane: 0,
    defaultColor: '#8b5cf6',
    params: [makeNumberParam('period', 'Period', '20')],
  },
  macd: {
    type: 'macd',
    name: 'MACD',
    category: 'momentum',
    defaultPane: 1,
    defaultColor: '#14a3bd',
    // Defaults for multi-series rendering
    // macd: main line, signal: signal line, histogram: bars/line
    // Consumers can override via IndicatorSetup.colors
    defaultColors: {
      macd: '#14a3bd',
      signal: '#e05252',
      histogramUp: '#2fae60',
      histogramDown: '#e05252',
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
    defaultColor: '#14a3bd',
    defaultColors: {
      rsi: '#14a3bd',
      upper: '#d97706',
      lower: '#b45309',
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


