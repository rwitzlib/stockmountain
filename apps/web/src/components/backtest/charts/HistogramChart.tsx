import { Histogram, niceStep, niceTicks } from '../../../utils/backtestAnalytics';

interface HistogramChartProps {
  histogram: Histogram;
  /** Bar color for a bin, given its lower edge */
  colorFor: (x0: number) => string;
  /** Axis label for a bin edge value */
  formatX: (x: number) => string;
  ariaLabel: string;
}

// Designed for a half-width (2-up) card, matching DailyPnlChart
const W = 560;
const H = 240;
const PAD = { l: 48, r: 10, t: 12, b: 26 };

export function HistogramChart({ histogram, colorFor, formatX, ariaLabel }: HistogramChartProps) {
  const { bins, binSize } = histogram;
  if (bins.length === 0) return null;

  const plotW = W - PAD.l - PAD.r;
  const plotH = H - PAD.t - PAD.b;
  const max = Math.max(...bins.map((b) => b.count), 1);
  const y = (v: number) => PAD.t + (1 - v / max) * plotH;
  const barW = plotW / bins.length;

  // X labels on nice edges roughly every quarter of the range
  const labelStep = Math.max(binSize, niceStep((bins.length * binSize) / 4 / binSize) * binSize);

  return (
    <svg viewBox={`0 0 ${W} ${H}`} className="block w-full h-auto" role="img" aria-label={ariaLabel}>
      {niceTicks(0, max, 3).map((t) =>
        t === 0 ? null : (
          <g key={t}>
            <line x1={PAD.l} x2={W - PAD.r} y1={y(t)} y2={y(t)} className="stroke-border" strokeWidth={1} />
            <text
              x={PAD.l - 5}
              y={y(t) + 3}
              textAnchor="end"
              className="fill-muted-foreground text-[10px] tabular-nums"
            >
              {t >= 1000 ? `${t / 1000}k` : t}
            </text>
          </g>
        )
      )}
      <line x1={PAD.l} x2={W - PAD.r} y1={PAD.t + plotH} y2={PAD.t + plotH} className="stroke-border" strokeWidth={1} />
      {bins.map((b, i) => {
        const h = Math.max((b.count / max) * plotH, b.count ? 1.5 : 0);
        return (
          <rect
            key={b.x0}
            x={PAD.l + i * barW + 1}
            y={PAD.t + plotH - h}
            width={Math.max(barW - 2, 1)}
            height={h}
            rx={1.5}
            fill={colorFor(b.x0)}
            opacity={0.85}
          />
        );
      })}
      {bins.map((b, i) =>
        Math.abs(b.x0 % labelStep) < binSize * 0.01 ? (
          <text
            key={`label-${b.x0}`}
            x={PAD.l + i * barW}
            y={H - 8}
            textAnchor="middle"
            className="fill-muted-foreground text-[10.5px] tabular-nums"
          >
            {formatX(b.x0)}
          </text>
        ) : null
      )}
    </svg>
  );
}
