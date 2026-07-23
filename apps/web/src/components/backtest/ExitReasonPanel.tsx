import { ExitReasonBreakdown } from '../../utils/backtestAnalytics';
import { formatSignedCurrency } from '../../utils/formatters';

export const EXIT_REASON_LABELS: Record<string, string> = {
  takeProfit: 'Target',
  stopLoss: 'Stop',
  timedExit: 'Timed',
  endOfData: 'Ended',
  soldAtHigh: 'High',
  manual: 'Manual',
};

const EXIT_REASON_HINTS: Record<string, string> = {
  takeProfit: 'profit target filled',
  stopLoss: 'stop loss filled',
  timedExit: 'rode the full exit window',
  endOfData: 'candles ran out early',
  soldAtHigh: 'sold at in-trade high',
  manual: 'closed by hand',
};

interface ExitReasonPanelProps {
  breakdown: ExitReasonBreakdown;
}

export function ExitReasonPanel({ breakdown }: ExitReasonPanelProps) {
  const { reasons, inferredCount, total } = breakdown;

  if (reasons.length === 0) {
    return <p className="text-sm text-muted-foreground">No exit data available.</p>;
  }

  const maxAbs = Math.max(...reasons.map((r) => Math.abs(r.pnl)), 1);
  const best = reasons.reduce((a, b) => (b.pnl > a.pnl ? b : a));
  const worst = reasons.reduce((a, b) => (b.pnl < a.pnl ? b : a));

  return (
    <div>
      {reasons.map((r, i) => {
        const widthPct = (Math.abs(r.pnl) / maxAbs) * 50;
        const share = total > 0 ? (r.count / total) * 100 : 0;
        return (
          <div
            key={r.reason}
            className={`grid grid-cols-[120px_1fr_140px] items-center gap-2.5 py-2 ${
              i > 0 ? 'border-t border-border/60' : ''
            }`}
          >
            <div>
              <div className="text-[13px]" title={EXIT_REASON_HINTS[r.reason]}>
                {EXIT_REASON_LABELS[r.reason] ?? r.reason}
              </div>
              <div className="text-[11px] text-muted-foreground tabular-nums">
                {r.count.toLocaleString()} · {share.toFixed(0)}% of exits
              </div>
            </div>
            <div className="relative h-[22px]">
              <span className="absolute left-1/2 -top-1 -bottom-1 w-px bg-border" />
              <span
                className="absolute top-[3px] h-4 min-w-[2px] rounded-[3px] opacity-85"
                style={{
                  width: `${widthPct}%`,
                  ...(r.pnl >= 0
                    ? { left: '50%', background: 'var(--chart-gain)' }
                    : { right: '50%', background: 'var(--chart-loss)' }),
                }}
              />
            </div>
            <div className="text-right">
              <span
                className="text-sm font-semibold tabular-nums"
                style={{ color: r.pnl >= 0 ? 'var(--chart-gain)' : 'var(--chart-loss)' }}
              >
                {formatSignedCurrency(r.pnl)}
              </span>
              <div className="text-[11px] text-muted-foreground tabular-nums">
                {formatSignedCurrency(r.count ? r.pnl / r.count : 0)}/trade
              </div>
            </div>
          </div>
        );
      })}

      <p className="mt-3 text-xs text-muted-foreground">
        <b className="text-foreground">{EXIT_REASON_LABELS[best.reason] ?? best.reason}</b> exits made{' '}
        <b
          className="tabular-nums"
          style={{ color: best.pnl >= 0 ? 'var(--chart-gain)' : 'var(--chart-loss)' }}
        >
          {formatSignedCurrency(best.pnl)}
        </b>{' '}
        across {best.count.toLocaleString()} trades
        {worst !== best && worst.pnl < 0 && (
          <>
            ; <b className="text-foreground">{EXIT_REASON_LABELS[worst.reason] ?? worst.reason}</b> exits gave
            back{' '}
            <b className="tabular-nums" style={{ color: 'var(--chart-loss)' }}>
              {formatSignedCurrency(worst.pnl)}
            </b>
          </>
        )}
        .
        {inferredCount > 0 && (
          <> Reasons inferred for {inferredCount.toLocaleString()} trades from an older result format.</>
        )}
      </p>
    </div>
  );
}
