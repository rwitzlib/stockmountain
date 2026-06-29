import { Line } from 'recharts';
import type { BarData } from '../../types/tools';

interface CandlestickSeriesProps {
  data: (BarData & { timestamp: string; color: string })[];
}

export function CandlestickSeries({ data }: CandlestickSeriesProps) {
  return (
    <>
      {data.map((entry, index) => (
        <g key={`candlestick-${index}`}>
          <Line
            type="linear"
            dataKey="h"
            stroke={entry.color}
            dot={false}
            data={[entry]}
          />
          <Line
            type="linear"
            dataKey="l"
            stroke={entry.color}
            dot={false}
            data={[entry]}
          />
          <Line
            type="linear"
            dataKey="o"
            stroke={entry.color}
            strokeWidth={8}
            dot={false}
            data={[entry]}
          />
          <Line
            type="linear"
            dataKey="c"
            stroke={entry.color}
            strokeWidth={8}
            dot={false}
            data={[entry]}
          />
        </g>
      ))}
    </>
  );
}