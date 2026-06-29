
import { ResponsiveContainer, LineChart, Line, XAxis, YAxis, Tooltip } from 'recharts';
import { formatDateTime } from '../../utils/dateFormatter';
import type { BarData } from '../../types/tools';

interface PriceChartProps {
  data: BarData[];
}

interface CustomTooltipProps {
  active?: boolean;
  payload?: any[];
  label?: string;
}

function CustomTooltip({ active, payload, label }: CustomTooltipProps) {
  if (!active || !payload?.length) return null;

  return (
    <div className="bg-white p-3 border border-gray-200 rounded-lg shadow-lg">
      <p className="font-medium">{label}</p>
      <p className="text-sm">
        Price: <span className="font-medium">${payload[0].value.toFixed(2)}</span>
      </p>
    </div>
  );
}

export function PriceChart({ data }: PriceChartProps) {
  // Add null check for data
  if (!data || !Array.isArray(data) || data.length === 0) {
    return (
      <div className="h-[400px] flex items-center justify-center bg-gray-50 rounded-lg">
        <p className="text-gray-500">No data available</p>
      </div>
    );
  }

  const chartData = data.map((bar) => ({
    timestamp: formatDateTime(new Date(bar.t)),
    price: bar.c
  }));

  return (
    <div className="h-[400px]">
      <ResponsiveContainer width="100%" height="100%">
        <LineChart
          data={chartData}
          margin={{ top: 20, right: 30, left: 60, bottom: 10 }}
        >
          <XAxis dataKey="timestamp" />
          <YAxis 
            domain={['auto', 'auto']}
            label={{ 
              value: 'Price ($)',
              angle: -90,
              position: 'insideLeft',
              offset: -45
            }}
          />
          <Tooltip content={<CustomTooltip />} />
          <Line
            type="monotone"
            dataKey="price"
            stroke="#2563eb"
            dot={false}
            strokeWidth={2}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}
