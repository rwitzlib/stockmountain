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
    : "p-4 bg-card/50 border border-border";
  
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
        <h3 className="text-[10px] font-mono uppercase tracking-wider text-primary dark:text-cyan-400 mb-2">
          # Balance History
        </h3>
        <div 
          className="flex items-center justify-center text-muted-foreground font-mono text-xs border border-dashed border-border"
          style={{ height: chartHeight }}
        >
          {error ? '⚠ Unable to load history' : '[ NO HISTORY DATA YET ]'}
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
  const axisColor = isDarkMode ? '#9ca3af' : '#6b7280';
  const gridColor = isDarkMode ? '#374151' : '#e5e7eb';
  const lineColor = isDarkMode ? '#22d3ee' : '#0891b2';
  const baselineColor = isDarkMode ? '#6b7280' : '#9ca3af';

  // Custom tooltip
  const CustomTooltip = ({ active, payload, label }: any) => {
    if (!active || !payload || !payload.length) return null;

    const data = payload[0].payload;
    const pnl = data.balance - startingBalance;
    const pnlPercent = ((pnl / startingBalance) * 100);

    return (
      <div className="bg-card border border-border p-3 shadow-lg">
        <div className="text-xs font-mono text-muted-foreground mb-2">{data.rawDate}</div>
        <div className="space-y-1">
          <div className="flex justify-between gap-4">
            <span className="text-xs font-mono text-muted-foreground">Balance:</span>
            <span className="text-xs font-mono font-bold text-primary dark:text-cyan-400">
              {formatPrice(data.balance)}
            </span>
          </div>
          <div className="flex justify-between gap-4">
            <span className="text-xs font-mono text-muted-foreground">Cash:</span>
            <span className="text-xs font-mono">{formatPrice(data.cash)}</span>
          </div>
          <div className="flex justify-between gap-4">
            <span className="text-xs font-mono text-muted-foreground">Unrealized:</span>
            <span className={`text-xs font-mono ${data.unrealized >= 0 ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'}`}>
              {data.unrealized >= 0 ? '+' : ''}{formatPrice(data.unrealized)}
            </span>
          </div>
          <div className="flex justify-between gap-4">
            <span className="text-xs font-mono text-muted-foreground">P/L:</span>
            <span className={`text-xs font-mono font-bold ${pnl >= 0 ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'}`}>
              {pnl >= 0 ? '+' : ''}{formatPrice(pnl)} ({pnlPercent >= 0 ? '+' : ''}{pnlPercent.toFixed(2)}%)
            </span>
          </div>
          {data.positions > 0 && (
            <div className="flex justify-between gap-4">
              <span className="text-xs font-mono text-muted-foreground">Positions:</span>
              <span className="text-xs font-mono text-yellow-600 dark:text-yellow-400">{data.positions}</span>
            </div>
          )}
        </div>
      </div>
    );
  };

  return (
    <div className={wrapperClass}>
      <h3 className={`font-mono uppercase tracking-wider text-primary dark:text-cyan-400 ${compact ? 'text-[10px] mb-2' : 'text-xs mb-4'}`}>
        # Balance History {!compact && '(Daily Snapshots)'}
      </h3>
      <div style={{ height: chartHeight }}>
        <ResponsiveContainer width="100%" height="100%">
          <LineChart 
            data={chartData}
            margin={compact ? { top: 5, right: 20, left: 40, bottom: 5 } : { top: 10, right: 30, left: 50, bottom: 10 }}
          >
            <XAxis 
              dataKey="date" 
              stroke={axisColor}
              tick={{ fill: axisColor, fontSize: compact ? 8 : 10, fontFamily: 'monospace' }}
              tickLine={!compact}
            />
            <YAxis 
              tickFormatter={(value) => formatPrice(value)}
              domain={['dataMin - 100', 'dataMax + 100']}
              stroke={axisColor}
              tick={{ fill: axisColor, fontSize: compact ? 8 : 10, fontFamily: 'monospace' }}
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
                fontSize: 10, 
                fontFamily: 'monospace' 
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

