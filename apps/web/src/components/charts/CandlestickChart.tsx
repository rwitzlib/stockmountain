
import { ResponsiveContainer, XAxis, YAxis, Tooltip, ComposedChart } from 'recharts';
import { CandlestickTooltip } from './CandlestickTooltip';
import { formatDateTime } from '../../utils/dateFormatter';
import type { BarData } from '../../types/tools';

interface CandlestickChartProps {
  data: BarData[];
}

export function CandlestickChart({ data }: CandlestickChartProps) {
  const chartData = data.map((bar) => ({
    ...bar,
    timestamp: formatDateTime(new Date(bar.t)),
    bodyStart: Math.min(bar.o, bar.c),
    bodyEnd: Math.max(bar.o, bar.c),
    color: bar.c >= bar.o ? '#16a34a' : '#dc2626'
  }));

  const yDomain = [
    Math.min(...data.map(d => d.l)) - 0.1,
    Math.max(...data.map(d => d.h)) + 0.1
  ];

  return (
    <div className="h-[400px]">
      <ResponsiveContainer width="100%" height="100%">
        <ComposedChart
          data={chartData}
          margin={{ top: 20, right: 30, left: 60, bottom: 10 }}
        >
          <XAxis dataKey="timestamp" />
          <YAxis domain={yDomain} />
          <Tooltip content={<CandlestickTooltip />} />
          
          {chartData.map((entry, index) => (
            <g key={`candlestick-${index}`}>
              {/* Wick */}
              <line
                x1={index * 30 + 15}
                y1={entry.l}
                x2={index * 30 + 15}
                y2={entry.h}
                stroke={entry.color}
                strokeWidth={1}
              />
              {/* Body */}
              <rect
                x={index * 30 + 11}
                y={Math.min(entry.o, entry.c)}
                width={8}
                height={Math.abs(entry.c - entry.o)}
                fill={entry.color}
              />
            </g>
          ))}
        </ComposedChart>
      </ResponsiveContainer>
    </div>
  );
}
