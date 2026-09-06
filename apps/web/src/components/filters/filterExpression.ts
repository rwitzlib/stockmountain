// Filter text is never assembled on the client: /filters/validate returns the canonical spelling
// plus spans, and a chip edit is a splice on a span followed by re-validation (plan 20). What
// remains here is the localStorage library of recents / pinned expressions and the templates.

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
    // storage full/unavailable: recents are best-effort
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

/** Named complete starting points for cold starts; inserted then modified. Written in canonical form. */
export const FILTER_TEMPLATES: { label: string; expression: string }[] = [
  { label: 'Oversold bounce', expression: 'rsi(14) < 30 [1m]' },
  { label: 'Volume surge', expression: 'volume > 1000000 [1d]' },
  { label: 'Above the 200-day', expression: 'close > sma(200) [1d]' },
  { label: 'Liquidity floor', expression: 'adv() > 2000000 [1d]' },
  { label: 'MACD turning bullish', expression: 'macd(12, 26, 9, ema).histogram > 0 [5m]' },
  { label: 'Above session VWAP', expression: 'close > vwap() [1m]' },
  { label: 'Held above VWAP', expression: 'close > vwap() [1m, 5, all]' },
];
