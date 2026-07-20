
import { useState, useEffect } from 'react';
import { Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { CustomTooltip } from './CustomTooltip';
import { DailyResult } from '../types/types';
import { formatDate } from '../utils/dateFormatter';
import { formatCurrency } from '../utils/formatters';
import { Card } from './ui/card';

interface BalanceChartProps {
  data: DailyResult[];
}

export function BalanceChart({ data }: BalanceChartProps) {
  const [isDarkMode, setIsDarkMode] = useState(() => 
    document.documentElement.classList.contains('dark')
  );

  // Detect theme changes
  useEffect(() => {
    const observer = new MutationObserver(() => {
      setIsDarkMode(document.documentElement.classList.contains('dark'));
    });
    observer.observe(document.documentElement, {
      attributes: true,
      attributeFilter: ['class'],
    });
    return () => observer.disconnect();
  }, []);

  // Check if any daily result has the "other" strategy
  const hasOtherStrategy = data.some(day => 'other' in day);

  const chartData = data.map(day => {
    const result: any = {
      date: formatDate(day.date),
      rawDate: day.date,
      hold: day.hold.totalBalance,
      holdTrades: {
        bought: day.hold.bought,
        sold: day.hold.sold
      },
      high: day.high.totalBalance,
      highTrades: {
        bought: day.high.bought,
        sold: day.high.sold
      }
    };

    // Add other strategy data if it exists
    if (day.other) {
      result.other = day.other.totalBalance;
      result.otherTrades = {
        bought: day.other.bought,
        sold: day.other.sold
      };
    }

    return result;
  });

  // Theme-aware colors
  const axisColor = isDarkMode ? '#8b93a1' : '#6b7280';

  return (
    <Card className="p-4 md:p-6">
      <h3 className="text-sm font-semibold tracking-tight text-foreground mb-4">Balance History</h3>
      <div className="h-[400px]">
        <ResponsiveContainer width="100%" height="100%">
          <LineChart 
            data={chartData}
            margin={{ top: 20, right: 30, left: 60, bottom: 10 }}
          >
            <XAxis 
              dataKey="date" 
              stroke={axisColor}
              tick={{ fill: axisColor, fontSize: 11 }}
            />
            <YAxis 
              tickFormatter={(value) => formatCurrency(value)}
              label={{ 
                value: 'Balance',
                angle: -90,
                position: 'insideLeft',
                offset: -45,
                style: { 
                  textAnchor: 'middle', 
                  fill: axisColor, 
                  fontSize: '11px'
                }
              }}
              domain={['dataMin - 500', 'dataMax + 500']}
              stroke={axisColor}
              tick={{ fill: axisColor, fontSize: 11 }}
            />
            <Tooltip content={<CustomTooltip />} />
            <Line 
              type="monotone" 
              dataKey="hold" 
              stroke="var(--chart-strategy)" 
              strokeWidth={2}
              name="Hold" 
              dot={{ fill: 'var(--chart-strategy)', r: 3 }} 
            />
            <Line 
              type="monotone" 
              dataKey="high" 
              stroke="#16a34a" 
              strokeWidth={2}
              name="High" 
              dot={{ fill: '#16a34a', r: 3 }} 
            />
            {hasOtherStrategy && (
              <Line 
                type="monotone" 
                dataKey="other" 
                stroke="#d97706" 
                strokeWidth={2}
                name="Other" 
                dot={{ fill: '#d97706', r: 3 }} 
              />
            )}
          </LineChart>
        </ResponsiveContainer>
      </div>
    </Card>
  );
}
