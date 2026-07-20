import { useMemo } from 'react';
import { BacktestEntry } from '../../types/backtest';
import { formatCurrency } from '../../utils/formatters';
import { formatDateNoTimezone } from '../../utils/dateFormatter';
import { BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, LineChart, Line, Cell } from 'recharts';
import { TrendingUp, TrendingDown, Award, AlertCircle } from 'lucide-react';

interface BacktestInsightsProps {
  results: BacktestEntry[];
}

interface ProfitDistributionData {
  range: string;
  count: number;
  min: number;
  max: number;
}

interface TimeSeriesData {
  date: string;
  profit: number;
  count: number;
}

export function BacktestInsights({ results }: BacktestInsightsProps) {
  const completedResults = results.filter(r => r.status === 'Completed');
  
  // Calculate profit distribution (histogram)
  const profitDistribution = useMemo((): ProfitDistributionData[] => {
    if (completedResults.length === 0) return [];
    
    const profits = completedResults.map(r => r.holdProfit || 0);
    const minProfit = Math.min(...profits);
    const maxProfit = Math.max(...profits);
    const range = maxProfit - minProfit;

    // All profits identical (or a single result) — one bucket avoids divide-by-zero / NaN index
    if (range === 0) {
      return [{
        range: formatCurrency(minProfit),
        count: profits.length,
        min: minProfit,
        max: maxProfit
      }];
    }

    const bucketCount = Math.min(10, Math.max(1, profits.length));
    const bucketSize = range / bucketCount;

    const buckets: ProfitDistributionData[] = Array.from({ length: bucketCount }, (_, i) => ({
      range: `${formatCurrency(minProfit + i * bucketSize)} - ${formatCurrency(minProfit + (i + 1) * bucketSize)}`,
      count: 0,
      min: minProfit + i * bucketSize,
      max: minProfit + (i + 1) * bucketSize
    }));

    profits.forEach(profit => {
      const bucketIndex = Math.min(
        Math.floor((profit - minProfit) / bucketSize),
        bucketCount - 1
      );
      if (buckets[bucketIndex]) {
        buckets[bucketIndex].count++;
      }
    });

    return buckets;
  }, [completedResults]);

  // Calculate time series data (backtests created over time)
  const timeSeriesData = useMemo((): TimeSeriesData[] => {
    if (results.length === 0) return [];

    const dataMap = new Map<string, { profit: number; count: number }>();
    
    results.forEach(result => {
      const date = formatDateNoTimezone(result.createdAt);
      const existing = dataMap.get(date) || { profit: 0, count: 0 };
      dataMap.set(date, {
        profit: existing.profit + (result.holdProfit || 0),
        count: existing.count + 1
      });
    });

    return Array.from(dataMap.entries())
      .map(([date, data]) => ({
        date,
        profit: data.profit / data.count, // Average profit per day
        count: data.count
      }))
      .sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime());
  }, [results]);

  // Calculate credits efficiency
  const creditsEfficiency = useMemo(() => {
    if (completedResults.length === 0) return null;
    
    const totalProfit = completedResults.reduce((sum, r) => sum + (r.holdProfit || 0), 0);
    const totalCredits = completedResults.reduce((sum, r) => sum + (r.creditsUsed || 0), 0);
    
    return totalCredits > 0 ? totalProfit / totalCredits : 0;
  }, [completedResults]);

  // Find best and worst performers (hold = realistic P/L)
  const bestPerformer = completedResults.length > 0
    ? completedResults.reduce((best, current) => 
        (current.holdProfit || 0) > (best.holdProfit || 0) ? current : best
      )
    : null;

  const worstPerformer = completedResults.length > 0
    ? completedResults.reduce((worst, current) => 
        (current.holdProfit || 0) < (worst.holdProfit || 0) ? current : worst
      )
    : null;

  if (completedResults.length === 0) {
    return (
      <div className="rounded-xl border border-border/80 bg-card p-6 text-center">
        <p className="text-sm text-muted-foreground">
          No completed backtests to analyze
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Performance Distribution Chart */}
      {profitDistribution.length > 0 && (
        <div className="rounded-xl border border-border/80 bg-card p-4">
          <h3 className="text-sm font-semibold tracking-tight text-foreground mb-4">
            Hold Profit Distribution
          </h3>
          <div className="h-64">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={profitDistribution} margin={{ top: 10, right: 10, left: 10, bottom: 60 }}>
                <XAxis 
                  dataKey="range" 
                  angle={-45}
                  textAnchor="end"
                  height={80}
                  tick={{ fontSize: 10, fill: '#8b93a1' }}
                />
                <YAxis tick={{ fontSize: 10, fill: '#8b93a1' }} />
                <Tooltip 
                  contentStyle={{ 
                    backgroundColor: 'hsl(var(--popover))',
                    border: '1px solid hsl(var(--border))',
                    borderRadius: '8px',
                    fontSize: '12px'
                  }}
                  formatter={(value: number) => [value, 'Count']}
                />
                <Bar dataKey="count" fill="var(--chart-strategy)">
                  {profitDistribution.map((entry, index) => (
                    <Cell 
                      key={`cell-${index}`} 
                      fill={entry.min >= 0 
                        ? 'var(--chart-gain)' 
                        : 'var(--chart-loss)'
                      } 
                    />
                  ))}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>
      )}

      {/* Time Series Chart */}
      {timeSeriesData.length > 0 && (
        <div className="rounded-xl border border-border/80 bg-card p-4">
          <h3 className="text-sm font-semibold tracking-tight text-foreground mb-4">
            Backtest Trend Over Time
          </h3>
          <div className="h-64">
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={timeSeriesData} margin={{ top: 10, right: 10, left: 10, bottom: 10 }}>
                <XAxis 
                  dataKey="date" 
                  tick={{ fontSize: 10, fill: '#8b93a1' }}
                  angle={-45}
                  textAnchor="end"
                  height={60}
                />
                <YAxis 
                  tick={{ fontSize: 10, fill: '#8b93a1' }}
                  tickFormatter={(value) => formatCurrency(value)}
                />
                <Tooltip 
                  contentStyle={{ 
                    backgroundColor: 'hsl(var(--popover))',
                    border: '1px solid hsl(var(--border))',
                    borderRadius: '8px',
                    fontSize: '12px'
                  }}
                  formatter={(value: number) => [formatCurrency(value), 'Avg Profit']}
                  labelFormatter={(label) => `Date: ${label}`}
                />
                <Line 
                  type="monotone" 
                  dataKey="profit" 
                  stroke="var(--chart-strategy)" 
                  strokeWidth={2}
                  dot={{ r: 3 }}
                />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </div>
      )}

      {/* Metrics Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* Credits Efficiency */}
        {creditsEfficiency !== null && (
          <div className="rounded-xl border border-border/80 bg-card p-4">
            <div className="flex items-center gap-2 mb-2">
              {creditsEfficiency > 0 ? (
                <TrendingUp className="h-4 w-4 text-green-600 dark:text-green-400" />
              ) : (
                <TrendingDown className="h-4 w-4 text-red-600 dark:text-red-400" />
              )}
              <h3 className="text-sm font-semibold tracking-tight text-foreground">
                Credits Efficiency
              </h3>
            </div>
            <div className={`text-2xl font-semibold tabular-nums ${
              creditsEfficiency > 0 
                ? 'text-green-600 dark:text-green-400' 
                : 'text-red-600 dark:text-red-400'
            }`}>
              {formatCurrency(creditsEfficiency)} / credit
            </div>
            <p className="text-xs text-muted-foreground mt-1">
              Average profit per credit used
            </p>
          </div>
        )}

        {/* Best Performer */}
        {bestPerformer && (
          <div className="rounded-xl border border-border/80 bg-card p-4">
            <div className="flex items-center gap-2 mb-2">
              <Award className="h-4 w-4 text-amber-600 dark:text-amber-400" />
              <h3 className="text-sm font-semibold tracking-tight text-foreground">
                Best Performer
              </h3>
            </div>
            <div className="space-y-1">
              <div className="text-xs text-muted-foreground">
                ID: <span className="font-mono text-foreground">{bestPerformer.id.substring(0, 12)}...</span>
              </div>
              <div className={`text-xl font-semibold tabular-nums ${
                (bestPerformer.holdProfit || 0) >= 0 
                  ? 'text-green-600 dark:text-green-400' 
                  : 'text-red-600 dark:text-red-400'
              }`}>
                {formatCurrency(bestPerformer.holdProfit || 0)}
              </div>
              <div className="text-xs text-muted-foreground">
                {formatDateNoTimezone(bestPerformer.createdAt)}
              </div>
            </div>
          </div>
        )}

        {/* Worst Performer */}
        {worstPerformer && (
          <div className="rounded-xl border border-border/80 bg-card p-4">
            <div className="flex items-center gap-2 mb-2">
              <AlertCircle className="h-4 w-4 text-red-600 dark:text-red-400" />
              <h3 className="text-sm font-semibold tracking-tight text-foreground">
                Worst Performer
              </h3>
            </div>
            <div className="space-y-1">
              <div className="text-xs text-muted-foreground">
                ID: <span className="font-mono text-foreground">{worstPerformer.id.substring(0, 12)}...</span>
              </div>
              <div className={`text-xl font-semibold tabular-nums ${
                (worstPerformer.holdProfit || 0) >= 0 
                  ? 'text-green-600 dark:text-green-400' 
                  : 'text-red-600 dark:text-red-400'
              }`}>
                {formatCurrency(worstPerformer.holdProfit || 0)}
              </div>
              <div className="text-xs text-muted-foreground">
                {formatDateNoTimezone(worstPerformer.createdAt)}
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

