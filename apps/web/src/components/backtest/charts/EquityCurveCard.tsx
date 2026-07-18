import { useMemo, useRef, useState } from 'react';
import { Card } from '../../ui/card';
import { EquityPoint } from '../../../types/types';
import { dateTicks, niceTicks } from '../../../utils/backtestAnalytics';
import {
  formatAxisCurrency,
  formatCurrency,
  formatShortDate,
  formatSignedCurrency,
  formatSignedPercent,
} from '../../../utils/formatters';

export interface EquitySeriesDef {
  key: string;
  name: string;
  /** CSS color, e.g. "var(--chart-strategy)" */
  color: string;
  dashed?: boolean;
  /** Balance per date, aligned to `dates`; null gaps are skipped */
  balances: (number | null)[];
  /** Filled area under the line (used for the primary series) */
  area?: boolean;
  /** Hidden until toggled on via the legend */
  defaultHidden?: boolean;
  /** Extra text on the legend button while the series is hidden */
  hiddenHint?: string;
}

interface EquityCurveCardProps {
  dates: string[];
  series: EquitySeriesDef[];
  startingBalance: number;
  /** Day-level detail for the crosshair tooltip (primary portfolio) */
  equity: EquityPoint[];
  drawdown: number[];
  footnote?: string;
}

const W = 920;
const H = 300;
const PAD = { l: 54, r: 122, t: 14, b: 26 };
const DD_H = 78;
const DD_PAD = { t: 6, b: 6 };

export function EquityCurveCard({
  dates,
  series,
  startingBalance,
  equity,
  drawdown,
  footnote,
}: EquityCurveCardProps) {
  const [mode, setMode] = useState<'pct' | 'usd'>('pct');
  const [hidden, setHidden] = useState<Record<string, boolean>>(() =>
    Object.fromEntries(series.filter((s) => s.defaultHidden).map((s) => [s.key, true]))
  );
  const [hoverIdx, setHoverIdx] = useState<number | null>(null);
  const [pointer, setPointer] = useState({ x: 0, y: 0 });
  const boxRef = useRef<HTMLDivElement>(null);

  const plotW = W - PAD.l - PAD.r;
  const plotH = H - PAD.t - PAD.b;
  const lastIdx = dates.length - 1;
  const x = (i: number) => PAD.l + (lastIdx > 0 ? (i / lastIdx) * plotW : plotW / 2);

  // ~62 viewBox units per label ("Feb 3" at 11px plus breathing room)
  const xTicks = useMemo(() => dateTicks(dates, Math.floor(plotW / 62)), [dates, plotW]);

  const toVal = (balance: number) =>
    mode === 'pct' ? (balance / startingBalance - 1) * 100 : balance;

  const visible = series.filter((s) => !hidden[s.key] && s.balances.some((b) => b != null));

  const { y, ticks } = useMemo(() => {
    let lo = Infinity;
    let hi = -Infinity;
    for (const s of visible) {
      for (const b of s.balances) {
        if (b == null) continue;
        const v = toVal(b);
        lo = Math.min(lo, v);
        hi = Math.max(hi, v);
      }
    }
    if (!Number.isFinite(lo)) {
      lo = 0;
      hi = 1;
    }
    const pad = (hi - lo) * 0.08 || 1;
    lo -= pad;
    hi += pad;
    return {
      y: (v: number) => PAD.t + (1 - (v - lo) / (hi - lo)) * plotH,
      ticks: niceTicks(lo, hi, 5),
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [series, hidden, mode, startingBalance]);

  // Direct labels at line ends; nudge apart when two series end close together
  const endLabels = useMemo(() => {
    const labels = visible
      .map((s) => {
        let i = s.balances.length - 1;
        while (i >= 0 && s.balances[i] == null) i--;
        if (i < 0) return null;
        const balance = s.balances[i] as number;
        return { series: s, cx: x(i), cy: y(toVal(balance)), value: toVal(balance), balance };
      })
      .filter((l): l is NonNullable<typeof l> => l != null)
      .sort((a, b) => a.cy - b.cy);
    for (let i = 1; i < labels.length; i++) {
      if (labels[i].cy - labels[i - 1].cy < 15) labels[i].cy = labels[i - 1].cy + 15;
    }
    return labels;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [visible, mode, y]);

  const zeroVal = mode === 'pct' ? 0 : startingBalance;
  const zeroInRange = ticks.length > 0 && zeroVal > ticks[0] - 1e-9 && y(zeroVal) > PAD.t && y(zeroVal) < PAD.t + plotH;

  const handleMove = (e: React.PointerEvent) => {
    const box = boxRef.current;
    if (!box || lastIdx < 0) return;
    const rect = box.getBoundingClientRect();
    const mx = ((e.clientX - rect.left) / rect.width) * W;
    const idx = Math.round(((mx - PAD.l) / plotW) * lastIdx);
    setHoverIdx(Math.max(0, Math.min(lastIdx, idx)));
    setPointer({ x: e.clientX - rect.left, y: e.clientY - rect.top });
  };

  const hoverDay = hoverIdx != null ? equity[hoverIdx] : null;

  // Drawdown strip geometry
  const ddPlotH = DD_H - DD_PAD.t - DD_PAD.b;
  const ddMin = Math.min(...drawdown, -0.01) * 1.12;
  const ddY = (v: number) => DD_PAD.t + (v / ddMin) * ddPlotH;
  const ddMinIdx = drawdown.indexOf(Math.min(...drawdown));
  const ddLine = drawdown
    .map((v, i) => `${i ? 'L' : 'M'}${x(i).toFixed(1)} ${ddY(v).toFixed(1)}`)
    .join(' ');

  return (
    <Card className="p-4 md:p-6">
      <div className="flex flex-wrap items-center gap-3 mb-3">
        <h2 className="text-sm font-semibold">Equity curve</h2>
        <div className="inline-flex rounded-lg border border-border overflow-hidden">
          {(['pct', 'usd'] as const).map((m) => (
            <button
              key={m}
              onClick={() => setMode(m)}
              className={`px-3 py-1 text-xs font-semibold transition-colors ${
                mode === m
                  ? 'bg-foreground text-background'
                  : 'text-muted-foreground hover:text-foreground'
              }`}
            >
              {m === 'pct' ? '%' : '$'}
            </button>
          ))}
        </div>
        <div className="ml-auto flex flex-wrap gap-1.5">
          {series.map((s) => {
            const off = !!hidden[s.key];
            return (
              <button
                key={s.key}
                aria-pressed={!off}
                onClick={() => setHidden((h) => ({ ...h, [s.key]: !h[s.key] }))}
                className={`inline-flex items-center gap-2 rounded-full border border-border px-3 py-1 text-xs text-muted-foreground transition-opacity hover:text-foreground ${
                  off ? 'opacity-45' : ''
                }`}
              >
                <span
                  className="h-[3px] w-3.5 rounded-sm"
                  style={{ background: off ? 'hsl(var(--muted-foreground))' : s.color }}
                />
                {s.name}
                {off && s.hiddenHint && (
                  <b className="font-semibold text-foreground">{s.hiddenHint}</b>
                )}
              </button>
            );
          })}
        </div>
      </div>

      <div
        ref={boxRef}
        className="relative"
        onPointerMove={handleMove}
        onPointerLeave={() => setHoverIdx(null)}
      >
        {/* overflow-visible lets long end-of-line labels spill into the card padding instead of clipping */}
        <svg viewBox={`0 0 ${W} ${H}`} className="block w-full h-auto overflow-visible" role="img" aria-label="Equity curve">
          {ticks.map((t) => (
            <g key={t}>
              <line
                x1={PAD.l}
                x2={W - PAD.r}
                y1={y(t)}
                y2={y(t)}
                className="stroke-border"
                strokeWidth={1}
              />
              <text
                x={PAD.l - 8}
                y={y(t) + 4}
                textAnchor="end"
                className="fill-muted-foreground text-[11px] tabular-nums"
              >
                {mode === 'pct' ? `${t.toFixed(0)}%` : formatAxisCurrency(t)}
              </text>
            </g>
          ))}
          {zeroInRange && (
            <line
              x1={PAD.l}
              x2={W - PAD.r}
              y1={y(zeroVal)}
              y2={y(zeroVal)}
              className="stroke-muted-foreground"
              strokeWidth={1}
              strokeDasharray="2 4"
            />
          )}
          {xTicks.map((t) => (
            <text
              key={t.index}
              x={x(t.index)}
              y={H - 8}
              textAnchor="middle"
              className="fill-muted-foreground text-[11px]"
            >
              {t.label}
            </text>
          ))}

          {visible.map((s) => {
            const pts = s.balances
              .map((b, i) => (b == null ? null : { px: x(i), py: y(toVal(b)) }))
              .filter((p): p is { px: number; py: number } => p != null);
            if (pts.length === 0) return null;
            const path = pts
              .map((p, i) => `${i ? 'L' : 'M'}${p.px.toFixed(1)} ${p.py.toFixed(1)}`)
              .join(' ');
            return (
              <g key={s.key}>
                {s.area && (
                  <path
                    d={`${path} L ${pts[pts.length - 1].px} ${PAD.t + plotH} L ${pts[0].px} ${PAD.t + plotH} Z`}
                    fill={s.color}
                    opacity={0.07}
                  />
                )}
                <path
                  d={path}
                  fill="none"
                  stroke={s.color}
                  strokeWidth={2}
                  strokeLinejoin="round"
                  strokeDasharray={s.dashed ? '5 4' : undefined}
                />
              </g>
            );
          })}

          {endLabels.map((l) => (
            <g key={l.series.key}>
              <circle cx={l.cx} cy={l.cy} r={3.5} fill={l.series.color} />
              <text
                x={l.cx + 9}
                y={l.cy + 4}
                className="fill-foreground text-xs font-semibold tabular-nums"
              >
                {mode === 'pct' ? formatSignedPercent(l.value, 1) : formatAxisCurrency(l.balance)}{' '}
                {l.series.name}
              </text>
            </g>
          ))}

          {hoverIdx != null && (
            <line
              x1={x(hoverIdx)}
              x2={x(hoverIdx)}
              y1={PAD.t}
              y2={PAD.t + plotH}
              className="stroke-muted-foreground"
              strokeWidth={1}
              strokeDasharray="3 3"
            />
          )}
        </svg>

        {hoverIdx != null && hoverDay && (
          <div
            className="pointer-events-none absolute z-10 min-w-[160px] rounded-lg border border-border bg-popover px-3 py-2 text-xs shadow-md"
            style={{
              left: Math.min(pointer.x + 14, (boxRef.current?.clientWidth ?? 400) - 180),
              top: Math.max(0, pointer.y - 10),
            }}
          >
            <div className="font-semibold mb-1">{formatShortDate(dates[hoverIdx])}</div>
            {visible.map((s) => {
              const b = s.balances[hoverIdx];
              if (b == null) return null;
              return (
                <div key={s.key} className="flex items-center justify-between gap-4">
                  <span className="inline-flex items-center gap-1.5 text-muted-foreground">
                    <span className="h-[7px] w-[7px] rounded-full" style={{ background: s.color }} />
                    {s.name}
                  </span>
                  <b className="tabular-nums">
                    {mode === 'pct' ? formatSignedPercent(toVal(b), 1) : formatCurrency(b)}
                  </b>
                </div>
              );
            })}
            <div className="mt-1 border-t border-border pt-1 flex items-center justify-between gap-4">
              <span className="text-muted-foreground">Day P&L</span>
              <b
                className="tabular-nums"
                style={{ color: hoverDay.dayProfit >= 0 ? 'var(--chart-gain)' : 'var(--chart-loss)' }}
              >
                {formatSignedCurrency(hoverDay.dayProfit)}
              </b>
            </div>
            <div className="flex items-center justify-between gap-4">
              <span className="text-muted-foreground">Trades</span>
              <b className="tabular-nums">{hoverDay.tradesTaken}</b>
            </div>
          </div>
        )}
      </div>

      <div className="mt-3 mb-0.5 text-[11px] uppercase tracking-widest text-muted-foreground">
        Drawdown
      </div>
      <svg
        viewBox={`0 0 ${W} ${DD_H}`}
        className="block w-full h-auto"
        role="img"
        aria-label="Drawdown from peak"
      >
        <line x1={PAD.l} x2={W - PAD.r} y1={DD_PAD.t} y2={DD_PAD.t} className="stroke-border" strokeWidth={1} />
        <text
          x={PAD.l - 8}
          y={DD_PAD.t + 10}
          textAnchor="end"
          className="fill-muted-foreground text-[11px] tabular-nums"
        >
          0%
        </text>
        {drawdown.length > 1 && (
          <>
            <path
              d={`${ddLine} L ${x(drawdown.length - 1)} ${DD_PAD.t} L ${x(0)} ${DD_PAD.t} Z`}
              fill="var(--chart-loss)"
              opacity={0.12}
            />
            <path d={ddLine} fill="none" stroke="var(--chart-loss)" strokeWidth={1.5} />
            <circle cx={x(ddMinIdx)} cy={ddY(drawdown[ddMinIdx])} r={3} fill="var(--chart-loss)" />
            <text
              x={x(ddMinIdx) + 7}
              y={ddY(drawdown[ddMinIdx]) + 4}
              className="text-[11px] font-semibold tabular-nums"
              fill="var(--chart-loss)"
            >
              {drawdown[ddMinIdx].toFixed(1)}%
            </text>
          </>
        )}
      </svg>

      {footnote && <p className="mt-3 text-xs text-muted-foreground">{footnote}</p>}
    </Card>
  );
}
