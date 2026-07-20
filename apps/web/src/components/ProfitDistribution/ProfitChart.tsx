
import { Bar, BarChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { formatCurrency } from '../../utils/formatters';
import { formatDateTimeWithHours } from '../../utils/dateFormatter';
import { ProfitData } from './types';

interface ProfitChartProps {
  data: ProfitData[];
}

interface CustomTooltipProps {
  active?: boolean;
  payload?: any[];
  label?: string;
}

function CustomTooltip({ active, payload, label }: CustomTooltipProps) {
  if (!active || !payload?.length) return null;

  const entry = payload[0].payload as ProfitData;
  
  return (
    <div className="rounded-lg border border-border bg-popover p-2 text-xs shadow-md">
      <div className="font-semibold text-foreground">Ticker: <span className="font-mono">{label}</span></div>
      <div className="text-muted-foreground">
        Bought at: {formatDateTimeWithHours(entry.boughtAt)}
      </div>
      <div className="mt-1 space-y-1">
        {payload.map((item, index) => (
          <div key={index} className="flex items-center gap-2">
            <div
              className="w-3 h-3 rounded-full"
              style={{ backgroundColor: item.fill }}
            />
            <span className="tabular-nums">{item.name}: {formatCurrency(item.value)}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

export function ProfitChart({ data }: ProfitChartProps) {
  // Check if any data has the "other" strategy
  const hasOtherStrategy = data.some(item => 'other' in item);

  return (
    <div className="h-[400px]">
      <ResponsiveContainer width="100%" height="100%">
        <BarChart
          data={data}
          margin={{ top: 20, right: 30, left: 60, bottom: 10 }}
        >
          <XAxis dataKey="ticker" tick={{ fill: '#8b93a1', fontSize: 11 }} />
          <YAxis
            tickFormatter={(value) => formatCurrency(value)}
            tick={{ fill: '#8b93a1', fontSize: 11 }}
            label={{
              value: 'Profit/Loss',
              angle: -90,
              position: 'insideLeft',
              offset: -45,
              style: { fill: '#8b93a1', fontSize: '11px' }
            }}
          />
          <Tooltip content={<CustomTooltip />} />
          <Bar dataKey="hold" fill="var(--chart-strategy)" name="Hold" />
          <Bar dataKey="high" fill="#16a34a" name="High" />
          {hasOtherStrategy && (
            <Bar dataKey="other" fill="#d97706" name="Other" />
          )}
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}
