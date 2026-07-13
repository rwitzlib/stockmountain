import { useMemo, useState } from 'react';
import { ExecutedTrade } from '../../types/types';
import { resolveExitReason, tradeDurationMinutes, tradeReturnPct } from '../../utils/backtestAnalytics';
import { formatSignedCurrency, formatSignedPercent } from '../../utils/formatters';
import { Button } from '../ui/button';

interface BacktestTradesTableProps {
  trades: ExecutedTrade[];
}

const PAGE_SIZE = 50;

function exitChip(trade: ExecutedTrade) {
  // Old results have no exitReason; resolveExitReason falls back to the legacy inference
  const reason = resolveExitReason(trade);

  if (reason === 'takeProfit') {
    return (
      <span className="rounded-full px-2 py-0.5 text-[10.5px] font-semibold uppercase tracking-wide"
        style={{ color: 'var(--chart-gain)', background: 'color-mix(in srgb, var(--chart-gain) 12%, transparent)' }}>
        Target
      </span>
    );
  }
  if (reason === 'stopLoss') {
    return (
      <span className="rounded-full px-2 py-0.5 text-[10.5px] font-semibold uppercase tracking-wide"
        style={{ color: 'var(--chart-loss)', background: 'color-mix(in srgb, var(--chart-loss) 12%, transparent)' }}>
        Stop
      </span>
    );
  }
  if (reason === 'endOfData') {
    return (
      <span className="rounded-full bg-muted px-2 py-0.5 text-[10.5px] font-semibold uppercase tracking-wide text-muted-foreground/60">
        Ended
      </span>
    );
  }
  if (reason === 'soldAtHigh') {
    return (
      <span className="rounded-full px-2 py-0.5 text-[10.5px] font-semibold uppercase tracking-wide"
        style={{ color: 'var(--chart-ceiling)', background: 'color-mix(in srgb, var(--chart-ceiling) 12%, transparent)' }}>
        High
      </span>
    );
  }
  return (
    <span className="rounded-full bg-muted px-2 py-0.5 text-[10.5px] font-semibold uppercase tracking-wide text-muted-foreground">
      Timed
    </span>
  );
}

function formatEntryTime(iso: string): string {
  // Show the timestamp in exchange time as recorded, not viewer-local time
  const m = /^(\d{4}-\d{2}-\d{2})T(\d{2}:\d{2})/.exec(iso);
  return m ? `${m[1]} · ${m[2]}` : iso;
}

export function BacktestTradesTable({ trades }: BacktestTradesTableProps) {
  const [query, setQuery] = useState('');
  const [visibleCount, setVisibleCount] = useState(PAGE_SIZE);

  const sorted = useMemo(
    () =>
      [...trades].sort((a, b) => new Date(b.boughtAt).getTime() - new Date(a.boughtAt).getTime()),
    [trades]
  );

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return sorted;
    return sorted.filter((t) => t.ticker.toLowerCase().includes(q));
  }, [sorted, query]);

  const visible = filtered.slice(0, visibleCount);

  return (
    <div>
      <div className="mb-2 flex flex-wrap items-center gap-3">
        <p className="text-xs text-muted-foreground tabular-nums">
          {filtered.length.toLocaleString()} of {trades.length.toLocaleString()} trades
        </p>
        <input
          type="text"
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            setVisibleCount(PAGE_SIZE);
          }}
          placeholder="Filter by ticker…"
          aria-label="Filter trades by ticker"
          className="ml-auto w-52 rounded-lg border border-border bg-background px-3 py-1.5 text-[13px] placeholder:text-muted-foreground focus:border-ring focus:outline-none"
        />
      </div>

      <div className="overflow-x-auto">
        <table className="w-full min-w-[980px] border-collapse text-[13px] tabular-nums">
          <thead>
            <tr>
              {['Ticker', 'Entry', 'Held', 'In', 'Out', 'Shares', 'Return', 'P&L', 'MFE', 'MAE', 'Exit'].map(
                (h, i) => (
                  <th
                    key={h}
                    className={`whitespace-nowrap border-b border-border px-2.5 pb-1.5 pt-2 text-[10.5px] font-semibold uppercase tracking-wider text-muted-foreground ${
                      i >= 3 && i <= 9 ? 'text-right' : 'text-left'
                    }`}
                  >
                    {h}
                  </th>
                )
              )}
            </tr>
          </thead>
          <tbody>
            {visible.length === 0 ? (
              <tr>
                <td colSpan={11} className="py-8 text-center text-xs text-muted-foreground">
                  No trades match this filter.
                </td>
              </tr>
            ) : (
              visible.map((t, i) => {
                const dur = Math.round(tradeDurationMinutes(t));
                const ret = tradeReturnPct(t);
                const pnlColor =
                  t.profit > 0 ? 'var(--chart-gain)' : t.profit < 0 ? 'var(--chart-loss)' : undefined;
                return (
                  <tr
                    key={`${t.ticker}-${t.boughtAt}-${i}`}
                    className="border-b border-border/60 hover:bg-muted/30"
                  >
                    <td className="whitespace-nowrap px-2.5 py-1.5 font-mono font-semibold">
                      {t.ticker}
                    </td>
                    <td className="whitespace-nowrap px-2.5 py-1.5 text-xs text-muted-foreground">
                      {formatEntryTime(t.boughtAt)}
                    </td>
                    <td className="whitespace-nowrap px-2.5 py-1.5">{dur}m</td>
                    <td className="whitespace-nowrap px-2.5 py-1.5 text-right">
                      ${t.startPrice.toFixed(2)}
                    </td>
                    <td className="whitespace-nowrap px-2.5 py-1.5 text-right">
                      ${t.endPrice.toFixed(2)}
                    </td>
                    <td className="whitespace-nowrap px-2.5 py-1.5 text-right">
                      {t.shares.toLocaleString()}
                    </td>
                    <td className="whitespace-nowrap px-2.5 py-1.5 text-right" style={{ color: pnlColor }}>
                      {formatSignedPercent(ret, 1)}
                    </td>
                    <td
                      className="whitespace-nowrap px-2.5 py-1.5 text-right font-semibold"
                      style={{ color: pnlColor }}
                    >
                      {formatSignedCurrency(t.profit)}
                    </td>
                    <td
                      className="whitespace-nowrap px-2.5 py-1.5 text-right"
                      style={{ color: t.maxRunup != null ? 'var(--chart-gain)' : undefined }}
                    >
                      {t.maxRunup != null ? formatSignedCurrency(t.maxRunup) : '—'}
                    </td>
                    <td
                      className="whitespace-nowrap px-2.5 py-1.5 text-right"
                      style={{ color: t.maxDrawdown != null ? 'var(--chart-loss)' : undefined }}
                    >
                      {t.maxDrawdown != null ? formatSignedCurrency(t.maxDrawdown) : '—'}
                    </td>
                    <td className="whitespace-nowrap px-2.5 py-1.5">{exitChip(t)}</td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {filtered.length > visibleCount && (
        <div className="mt-3 text-center">
          <Button
            variant="outline"
            size="sm"
            onClick={() => setVisibleCount((c) => c + 200)}
          >
            Show more ({(filtered.length - visibleCount).toLocaleString()} remaining)
          </Button>
        </div>
      )}
    </div>
  );
}
