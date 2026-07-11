import { useState } from 'react';
import { TickerAggregate } from '../../utils/backtestAnalytics';
import { formatSignedCurrency } from '../../utils/formatters';

interface TickerLeadersPanelProps {
  best: TickerAggregate[];
  worst: TickerAggregate[];
}

export function TickerLeadersPanel({ best, worst }: TickerLeadersPanelProps) {
  const [tab, setTab] = useState<'best' | 'worst'>('best');
  const list = tab === 'best' ? best : worst;
  const maxAbs = Math.max(...list.map((t) => Math.abs(t.pnl)), 1);

  return (
    <div>
      <div className="mb-2 flex justify-end gap-1">
        {(['best', 'worst'] as const).map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`rounded-full border px-3 py-1 text-xs font-semibold transition-colors ${
              tab === t
                ? 'border-foreground bg-foreground text-background'
                : 'border-border text-muted-foreground hover:text-foreground'
            }`}
          >
            {t === 'best' ? 'Top' : 'Bottom'}
          </button>
        ))}
      </div>
      {list.map((t, i) => (
        <div
          key={t.ticker}
          className={`grid grid-cols-[64px_1fr_150px] items-center gap-2.5 py-1.5 ${
            i > 0 ? 'border-t border-border/60' : ''
          }`}
        >
          <span className="font-mono text-[13px] font-semibold">{t.ticker}</span>
          <div>
            <div
              className="h-2.5 min-w-[2px] rounded-[3px] opacity-80"
              style={{
                width: `${(Math.abs(t.pnl) / maxAbs) * 100}%`,
                background: t.pnl >= 0 ? 'var(--chart-gain)' : 'var(--chart-loss)',
              }}
            />
          </div>
          <div className="text-right text-[13px] tabular-nums">
            <span
              className="font-semibold"
              style={{ color: t.pnl >= 0 ? 'var(--chart-gain)' : 'var(--chart-loss)' }}
            >
              {formatSignedCurrency(t.pnl)}
            </span>
            <span className="text-[11px] text-muted-foreground"> · {t.wins}/{t.trades} won</span>
          </div>
        </div>
      ))}
    </div>
  );
}
