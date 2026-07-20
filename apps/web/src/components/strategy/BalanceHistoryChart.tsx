import { useState, useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis, ReferenceLine } from 'recharts';
import { strategyApi } from '../../api/strategyApi';
import { formatPrice } from '../../utils/chartUtils';

interface BalanceHistoryChartProps {
  strategyId: string;
  startingBalance: number;
  compact?: boolean;
}

export function BalanceHistoryChart({ strategyId, startingBalance, compact = false }: BalanceHistoryChartProps) {
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

  const { data: balanceHistory, isLoading, error } = useQuery({
    queryKey: ['balanceHistory', strategyId],
    queryFn: () => strategyApi.getBalanceHistory(strategyId),
    refetchInterval: 300000, // Refresh every 5 minutes
    enabled: !!strategyId,
  });

  const wrapperClass = compact 
    ? "p-3" 
    : "p-4 rounded-xl border border-border/80 bg-card";
  
  const chartHeight = compact ? 140 : 200;

  if (isLoading) {
    return (
      <div className={wrapperClass}>
        <div className="animate-pulse">
          <div className="h-3 bg-muted rounded w-1/4 mb-3"></div>
          <div className={`bg-muted rounded`} style={{ height: chartHeight }}></div>
        </div>
      </div>
    );
  }

  if (error || !balanceHistory?.history || balanceHistory.history.length === 0) {
    return (
      <div className={wrapperClass}>
        <h3 className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground mb-2">
          Balance History
        </h3>
        <div 
          className="flex items-center justify-center text-muted-foreground text-xs border border-dashed border-border rounded-lg"
          style={{ height: chartHeight }}
        >
          {error ? 'Unable to load history' : 'No history data yet'}
        </div>
      </div>
    );
  }

  // Transform data for the chart
  const chartData = balanceHistory.history.map(entry => ({
    date: new Date(entry.date).toLocaleDateString('en-US', { month: 'short', day: 'numeric' }),
    rawDate: entry.date,
    balance: entry.currentBalance,
    cash: entry.cashBalance,
    unrealized: entry.unrealizedPnl,
    positions: entry.openPositionsCount,
    snapshotType: entry.snapshotType
  }));

  // Theme-aware colors
  const axisColor = '#8b93a1';
  const gridColor = 'rgba(148,163,184,0.1)';
  const lineColor = 'var(--chart-strategy)';
  const baselineColor = isDarkMode ? '#6b7280' : '#9ca3af';

  // Custom tooltip
  const CustomTooltip = ({ active, payload, label }: any) => {
    if (!active || !payload || !payload.length) return null;

    const data = payload[0].payload;
    const pnl = data.balance - startingBalance;
    const pnlPercent = ((pnl / startingBalance) * 100);

    return (
      <div className="rounded-lg border border-border p-3 shadow-sm" style={{ backgroundColor: 'hsl(var(--popover))' }}>
        <div className="text-xs text-muted-foreground mb-2">{data.rawDate}</div>
        <div className="space-y-1">
          <div className="flex justify-between gap-4">
            <span className="text-xs text-muted-foreground">Balance:</span>
            <span className="text-xs font-semibold text-foreground tabular-nums">
              {formatPrice(data.balance)}
            </span>
          </div>
          <div className="flex justify-between gap-4">
            <span className="text-xs text-muted-foreground">Cash:</span>
            <span className="text-xs tabular-nums">{formatPrice(data.cash)}</span>
          </div>
          <div className="flex justify-between gap-4">
            <span className="text-xs text-muted-foreground">Unrealized:</span>
            <span className={`text-xs tabular-nums ${data.unrealized >= 0 ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'}`}>
              {data.unrealized >= 0 ? '+' : ''}{formatPrice(data.unrealized)}
            </span>
          </div>
          <div className="flex justify-between gap-4">
            <span className="text-xs text-muted-foreground">P/L:</span>
            <span className={`text-xs font-semibold tabular-nums ${pnl >= 0 ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'}`}>
              {pnl >= 0 ? '+' : ''}{formatPrice(pnl)} ({pnlPercent >= 0 ? '+' : ''}{pnlPercent.toFixed(2)}%)
            </span>
          </div>
          {data.positions > 0 && (
            <div className="flex justify-between gap-4">
              <span className="text-xs text-muted-foreground">Positions:</span>
              <span className="text-xs text-yellow-600 dark:text-yellow-400 tabular-nums">{data.positions}</span>
            </div>
          )}
        </div>
      </div>
    );
  };

  return (
    <div className={wrapperClass}>
      <h3 className={`text-[11px] font-medium uppercase tracking-widest text-muted-foreground ${compact ? 'mb-2' : 'mb-4'}`}>
        Balance History {!compact && '(Daily Snapshots)'}
      </h3>
      <div style={{ height: chartHeight }}>
        <ResponsiveContainer width="100%" height="100%">
          <LineChart 
            data={chartData}
            margin={compact ? { top: 5, right: 20, left: 40, bottom: 5 } : { top: 10, right: 30, left: 50, bottom: 10 }}
          >
            <XAxis 
              dataKey="date" 
              stroke={gridColor}
              tick={{ fill: axisColor, fontSize: compact ? 8 : 10 }}
              tickLine={!compact}
            />
            <YAxis 
              tickFormatter={(value) => formatPrice(value)}
              domain={['dataMin - 100', 'dataMax + 100']}
              stroke={gridColor}
              tick={{ fill: axisColor, fontSize: compact ? 8 : 10 }}
              tickLine={!compact}
              width={compact ? 55 : 70}
            />
            <Tooltip content={<CustomTooltip />} />
            <ReferenceLine 
              y={startingBalance} 
              stroke={baselineColor} 
              strokeDasharray="3 3" 
              label={!compact ? { 
                value: 'Starting', 
                position: 'right', 
                fill: baselineColor, 
                fontSize: 10 
              } : undefined} 
            />
            <Line 
              type="monotone" 
              dataKey="balance" 
              stroke={lineColor} 
              strokeWidth={2}
              dot={compact ? false : { fill: lineColor, r: 3 }}
              activeDot={{ r: compact ? 4 : 5 }}
            />
          </LineChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}

