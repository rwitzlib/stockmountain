import type { FilterAstNode, FilterTimeframe } from '../../types/filters';

/** Short timeframe form used in range brackets: {multiplier}{unit}, e.g. "5m", "1d". */
export function formatTimeframe(tf: FilterTimeframe): string {
  const unit =
    { minute: 'm', hour: 'h', day: 'd', week: 'w', month: 'mo', quarter: 'q', year: 'y' }[
      tf.timespan
    ] ?? tf.timespan;
  return `${tf.multiplier}${unit}`;
}

/**
 * Serializes a presentation AST back to DSL text. Must round-trip anything the
 * validate endpoint emits — chip edits re-serialize the whole tree.
 */
export function serializeAst(node: FilterAstNode): string {
  switch (node.kind) {
    case 'range': {
      const inner = serializeAst(node.inner!);
      const tf = node.timeframe ? formatTimeframe(node.timeframe) : '';
      if (tf && node.candles) return `${inner} [${tf}, ${node.candles}]`;
      if (tf) return `${inner} [${tf}]`;
      if (node.candles) return `${inner} [, ${node.candles}]`;
      return inner;
    }
    case 'binary':
      return `${serializeAst(node.left!)} ${node.op} ${serializeAst(node.right!)}`;
    case 'function':
      return `${node.name}(${(node.args ?? []).map(serializeAst).join(', ')})`;
    case 'field':
      return `${serializeAst(node.target!)}.${node.field}`;
    case 'data':
      return node.field ?? '';
    case 'literal':
    case 'raw':
    default:
      return node.value ?? '';
  }
}

// ---- Recents / pinned library (localStorage, phase 3) ----

export interface FilterLibraryEntry {
  expression: string;
  lastUsed: number;
  pinned: boolean;
}

const LIBRARY_KEY = 'mv-filter-library';
const LIBRARY_CAP = 50;

export function loadFilterLibrary(): FilterLibraryEntry[] {
  try {
    const raw = localStorage.getItem(LIBRARY_KEY);
    return raw ? (JSON.parse(raw) as FilterLibraryEntry[]) : [];
  } catch {
    return [];
  }
}

function saveFilterLibrary(entries: FilterLibraryEntry[]) {
  try {
    localStorage.setItem(LIBRARY_KEY, JSON.stringify(entries.slice(0, LIBRARY_CAP)));
  } catch {
    // storage full/unavailable — recents are best-effort
  }
}

export function recordFilterUse(expression: string) {
  const normalized = expression.trim();
  if (!normalized) return;
  const entries = loadFilterLibrary();
  const existing = entries.find((e) => e.expression === normalized);
  if (existing) {
    existing.lastUsed = Date.now();
  } else {
    entries.push({ expression: normalized, lastUsed: Date.now(), pinned: false });
  }
  entries.sort((a, b) => Number(b.pinned) - Number(a.pinned) || b.lastUsed - a.lastUsed);
  saveFilterLibrary(entries);
}

export function toggleFilterPin(expression: string) {
  const entries = loadFilterLibrary();
  const entry = entries.find((e) => e.expression === expression);
  if (entry) {
    entry.pinned = !entry.pinned;
    entries.sort((a, b) => Number(b.pinned) - Number(a.pinned) || b.lastUsed - a.lastUsed);
    saveFilterLibrary(entries);
  }
}

/** Named complete starting points for cold starts; inserted then modified. */
export const FILTER_TEMPLATES: { label: string; expression: string }[] = [
  { label: 'Oversold bounce', expression: 'rsi(14) < 30 [1m]' },
  { label: 'Volume surge', expression: 'volume > 1000000 [1d]' },
  { label: 'Above the 200-day', expression: 'close > sma(200) [1d]' },
  { label: 'Liquidity floor', expression: 'adv() > 2000000 [1d]' },
  { label: 'MACD turning bullish', expression: 'macd(12,26,9,ema).histogram > 0 [5m]' },
];
