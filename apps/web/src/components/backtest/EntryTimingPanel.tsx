import { EntryTimeBucket } from '../../utils/backtestAnalytics';
import { formatSignedCurrency } from '../../utils/formatters';

interface EntryTimingPanelProps {
  buckets: EntryTimeBucket[];
}

export function EntryTimingPanel({ buckets }: EntryTimingPanelProps) {
  if (buckets.length === 0) {
    return <p className="text-sm text-muted-foreground">No entry-time data available.</p>;
  }

  const maxAbs = Math.max(...buckets.map((b) => Math.abs(b.pnl)), 1);
  const best = buckets.reduce((a, b) => (b.pnl > a.pnl ? b : a));
  const late = buckets.filter((b) => b.label === '11:00–14:00' || b.label === 'After 14:00');
  const lateNet = late.reduce((sum, b) => sum + b.pnl, 0);

  return (
    <div>
      {buckets.map((b, i) => {
        const widthPct = (Math.abs(b.pnl) / maxAbs) * 50;
        return (
          <div
            key={b.label}
            className={`grid grid-cols-[96px_1fr_130px] items-center gap-2.5 py-2 ${
              i > 0 ? 'border-t border-border/60' : ''
            }`}
          >
            <div>
              <div className="text-[13px]">{b.label}</div>
              <div className="text-[11px] text-muted-foreground tabular-nums">
                {b.count.toLocaleString()} trades
              </div>
            </div>
            <div className="relative h-[22px]">
              <span className="absolute left-1/2 -top-1 -bottom-1 w-px bg-border" />
              <span
                className="absolute top-[3px] h-4 rounded-[3px] opacity-85"
                style={{
                  width: `${widthPct}%`,
                  ...(b.pnl >= 0
                    ? { left: '50%', background: 'var(--chart-gain)' }
                    : { right: '50%', background: 'var(--chart-loss)' }),
                }}
              />
            </div>
            <div className="text-right">
              <span
                className="text-sm font-semibold tabular-nums"
                style={{ color: b.pnl >= 0 ? 'var(--chart-gain)' : 'var(--chart-loss)' }}
              >
                {formatSignedCurrency(b.pnl)}
              </span>
              <div className="text-[11px] text-muted-foreground tabular-nums">
                {b.winRate.toFixed(1)}% win
              </div>
            </div>
          </div>
        );
      })}

      <p className="mt-3 text-xs text-muted-foreground">
        Best window: <b className="text-foreground">{best.label}</b> with{' '}
        <b className="tabular-nums" style={{ color: best.pnl >= 0 ? 'var(--chart-gain)' : 'var(--chart-loss)' }}>
          {formatSignedCurrency(best.pnl)}
        </b>{' '}
        across {best.count.toLocaleString()} entries ({best.winRate.toFixed(1)}% win)
        {late.length > 0 && lateNet < 0 && (
          <>
            {' '}
            — entries after 11:00 gave back{' '}
            <b className="tabular-nums" style={{ color: 'var(--chart-loss)' }}>
              {formatSignedCurrency(lateNet)}
            </b>{' '}
            net.
          </>
        )}
      </p>
    </div>
  );
}
