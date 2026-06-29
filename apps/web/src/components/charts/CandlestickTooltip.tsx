import { formatDateTime } from '../../utils/dateFormatter';
import type { BarData } from '../../types/tools';

interface CandlestickTooltipProps {
  active?: boolean;
  payload?: Array<{ payload: BarData & { timestamp: string } }>;
}

export function CandlestickTooltip({ active, payload }: CandlestickTooltipProps) {
  if (!active || !payload?.length) return null;

  const data = payload[0].payload;

  return (
    <div className="bg-white p-3 border border-gray-200 rounded-lg shadow-lg">
      <p className="font-medium mb-2">{formatDateTime(new Date(data.t))}</p>
      <div className="space-y-1 text-sm">
        <p>Open: <span className="font-medium">${data.o.toFixed(2)}</span></p>
        <p>High: <span className="font-medium">${data.h.toFixed(2)}</span></p>
        <p>Low: <span className="font-medium">${data.l.toFixed(2)}</span></p>
        <p>Close: <span className="font-medium">${data.c.toFixed(2)}</span></p>
        <p>Volume: <span className="font-medium">{data.v.toLocaleString()}</span></p>
      </div>
    </div>
  );
}