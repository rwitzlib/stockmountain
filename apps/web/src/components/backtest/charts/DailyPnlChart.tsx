import { useMemo, useRef, useState } from 'react';
import { EquityPoint } from '../../../types/types';
import { binDailyPnl, dateTicks, niceTicks, PnlBucket } from '../../../utils/backtestAnalytics';
import {
  formatAxisCurrency,
  formatCurrency,
  formatShortDate,
  formatSignedCurrency,
} from '../../../utils/formatters';

interface DailyPnlChartProps {
  equity: EquityPoint[];
}

const W = 560;
const H = 240;
const PAD = { l: 48, r: 10, t: 12, b: 26 };

export function DailyPnlChart({ equity }: DailyPnlChartProps) {
  const [hoverIdx, setHoverIdx] = useState<number | null>(null);
  const boxRef = useRef<HTMLDivElement>(null);

  const plotW = W - PAD.l - PAD.r;
  const plotH = H - PAD.t - PAD.b;

  // A bar plus its gap needs ~7 viewBox units to stay distinguishable; beyond
  // that the days roll up to weekly, then monthly totals.
  const { unit, buckets } = useMemo(() => binDailyPnl(equity, Math.floor(plotW / 7)), [equity, plotW]);

  // ~62 viewBox units per label ("Feb 3" at 10.5px plus breathing room)
  const xTicks = useMemo(
    () => dateTicks(buckets.map((b) => b.date), Math.floor(plotW / 62)),
    [buckets, plotW]
  );

  if (buckets.length === 0) return null;

  const n = buckets.length;
  let lo = Math.min(...buckets.map((b) => b.pnl), 0);
  let hi = Math.max(...buckets.map((b) => b.pnl), 0);
  const pad = (hi - lo) * 0.1 || 1;
  lo -= pad;
  hi += pad;

  const y = (v: number) => PAD.t + (1 - (v - lo) / (hi - lo)) * plotH;
  const barW = Math.max(1.5, Math.min(18, plotW / n - 4));
  const cx = (i: number) => PAD.l + ((i + 0.5) / n) * plotW;

  const bucketLabel = (b: PnlBucket) => {
    if (unit === 'week') return `Week of ${formatShortDate(b.date)}`;
    if (unit === 'month') {
      return new Date(`${b.date}T12:00:00`).toLocaleDateString('en-US', {
        month: 'short',
        year: 'numeric',
      });
    }
    return formatShortDate(b.date);
  };

  const handleMove = (e: React.PointerEvent) => {
    const box = boxRef.current;
    if (!box) return;
    const rect = box.getBoundingClientRect();
    const mx = ((e.clientX - rect.left) / rect.width) * W;
    const idx = Math.floor(((mx - PAD.l) / plotW) * n);
    setHoverIdx(Math.max(0, Math.min(n - 1, idx)));
  };

  const hover = hoverIdx != null ? buckets[hoverIdx] : null;

  return (
    <div ref={boxRef} className="relative" onPointerMove={handleMove} onPointerLeave={() => setHoverIdx(null)}>
      <svg viewBox={`0 0 ${W} ${H}`} className="block w-full h-auto" role="img" aria-label="Daily profit and loss">
        {niceTicks(lo, hi, 4).map((t) => (
          <g key={t}>
            <line x1={PAD.l} x2={W - PAD.r} y1={y(t)} y2={y(t)} className="stroke-border" strokeWidth={1} />
            <text
              x={PAD.l - 6}
              y={y(t) + 4}
              textAnchor="end"
              className="fill-muted-foreground text-[10.5px] tabular-nums"
            >
              {formatAxisCurrency(t)}
            </text>
          </g>
        ))}
        <line x1={PAD.l} x2={W - PAD.r} y1={y(0)} y2={y(0)} className="stroke-muted-foreground" strokeWidth={1} />
        {buckets.map((b, i) => (
          <rect
            key={b.date}
            x={cx(i) - barW / 2}
            y={b.pnl >= 0 ? y(b.pnl) : y(0)}
            width={barW}
            height={Math.max(Math.abs(y(b.pnl) - y(0)), 1)}
            rx={barW > 3 ? 2 : 0}
            fill={b.pnl >= 0 ? 'var(--chart-gain)' : 'var(--chart-loss)'}
            opacity={hoverIdx === i ? 1 : 0.85}
          />
        ))}
        {xTicks.map((t) => (
          <text
            key={t.index}
            x={cx(t.index)}
            y={H - 8}
            textAnchor="middle"
            className="fill-muted-foreground text-[10.5px]"
          >
            {t.label}
          </text>
        ))}
      </svg>

      {unit !== 'day' && (
        <p className="mt-1 text-[11px] text-muted-foreground">
          Aggregated to {unit === 'week' ? 'weekly' : 'monthly'} totals · {n} {unit === 'week' ? 'weeks' : 'months'}
        </p>
      )}

      {hover && hoverIdx != null && (
        <div
          className="pointer-events-none absolute z-10 min-w-[150px] rounded-lg border border-border bg-popover px-3 py-2 text-xs shadow-md"
          style={{
            left: `${Math.min((cx(hoverIdx) / W) * 100, 68)}%`,
            top: 0,
          }}
        >
          <div className="font-semibold mb-1">{bucketLabel(hover)}</div>
          <div className="flex items-center justify-between gap-4">
            <span className="text-muted-foreground">P&L</span>
            <b
              className="tabular-nums"
              style={{ color: hover.pnl >= 0 ? 'var(--chart-gain)' : 'var(--chart-loss)' }}
            >
              {formatSignedCurrency(hover.pnl)}
            </b>
          </div>
          <div className="flex items-center justify-between gap-4">
            <span className="text-muted-foreground">Trades</span>
            <b className="tabular-nums">{hover.tradesTaken}</b>
          </div>
          <div className="flex items-center justify-between gap-4">
            <span className="text-muted-foreground">Balance</span>
            <b className="tabular-nums">{formatCurrency(hover.totalBalance)}</b>
          </div>
        </div>
      )}
    </div>
  );
}
