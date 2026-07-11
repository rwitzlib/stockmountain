import { useRef, useState } from 'react';
import { EquityPoint } from '../../../types/types';
import { niceTicks } from '../../../utils/backtestAnalytics';
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

  if (equity.length === 0) return null;

  const plotW = W - PAD.l - PAD.r;
  const plotH = H - PAD.t - PAD.b;

  let lo = Math.min(...equity.map((d) => d.dayProfit), 0);
  let hi = Math.max(...equity.map((d) => d.dayProfit), 0);
  const pad = (hi - lo) * 0.1 || 1;
  lo -= pad;
  hi += pad;

  const y = (v: number) => PAD.t + (1 - (v - lo) / (hi - lo)) * plotH;
  const barW = Math.min(18, plotW / equity.length - 4);
  const cx = (i: number) => PAD.l + ((i + 0.5) / equity.length) * plotW;
  const hover = hoverIdx != null ? equity[hoverIdx] : null;

  return (
    <div ref={boxRef} className="relative">
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
        {equity.map((d, i) => (
          <rect
            key={d.date}
            x={cx(i) - barW / 2}
            y={d.dayProfit >= 0 ? y(d.dayProfit) : y(0)}
            width={barW}
            height={Math.max(Math.abs(y(d.dayProfit) - y(0)), 1)}
            rx={2}
            fill={d.dayProfit >= 0 ? 'var(--chart-gain)' : 'var(--chart-loss)'}
            opacity={hoverIdx === i ? 1 : 0.85}
            onPointerEnter={() => setHoverIdx(i)}
            onPointerLeave={() => setHoverIdx(null)}
          />
        ))}
        {equity.map((d, i) =>
          i === equity.length - 1 || (i % 4 === 0 && equity.length - 1 - i >= 3) ? (
            <text
              key={`label-${d.date}`}
              x={cx(i)}
              y={H - 8}
              textAnchor="middle"
              className="fill-muted-foreground text-[10.5px]"
            >
              {formatShortDate(d.date)}
            </text>
          ) : null
        )}
      </svg>

      {hover && hoverIdx != null && (
        <div
          className="pointer-events-none absolute z-10 min-w-[150px] rounded-lg border border-border bg-popover px-3 py-2 text-xs shadow-md"
          style={{
            left: `${Math.min((cx(hoverIdx) / W) * 100, 68)}%`,
            top: 0,
          }}
        >
          <div className="font-semibold mb-1">{formatShortDate(hover.date)}</div>
          <div className="flex items-center justify-between gap-4">
            <span className="text-muted-foreground">P&L</span>
            <b
              className="tabular-nums"
              style={{ color: hover.dayProfit >= 0 ? 'var(--chart-gain)' : 'var(--chart-loss)' }}
            >
              {formatSignedCurrency(hover.dayProfit)}
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
