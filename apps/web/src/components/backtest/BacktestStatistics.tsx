import { BacktestEntry } from '../../types/backtest';
import { TrendingUp, TrendingDown, Award, CheckCircle2, XCircle } from 'lucide-react';
import { formatCurrency } from '../../utils/formatters';

interface BacktestStatisticsProps {
  results: BacktestEntry[];
}

export const BacktestStatistics = ({ results }: BacktestStatisticsProps) => {
  // Calculate basic statistics
  const totalBacktests = results.length;
  const totalCreditsUsed = results.reduce((sum, result) => sum + (result.creditsUsed || 0), 0);
  const completedBacktests = results.filter(result => result.status === 'Completed').length;
  const inProgressBacktests = results.filter(result => result.status === 'InProgress').length;
  const failedBacktests = results.filter(result => result.status === 'Failed' || result.status === 'Error').length;

  // Calculate additional metrics
  const completedResults = results.filter(result => result.status === 'Completed');
  const avgHoldProfit = completedResults.length > 0
    ? completedResults.reduce((sum, result) => sum + (result.holdProfit || 0), 0) / completedResults.length
    : 0;
  const avgHighProfit = completedResults.length > 0
    ? completedResults.reduce((sum, result) => sum + (result.highProfit || 0), 0) / completedResults.length
    : 0;
  const avgCreditsPerBacktest = totalBacktests > 0 ? totalCreditsUsed / totalBacktests : 0;
  const successRate = totalBacktests > 0 ? (completedBacktests / totalBacktests) * 100 : 0;

  // Find best performing backtest (using holdProfit - realistic performance)
  // Note: highProfit is theoretical upper bound, not realistic
  const bestBacktest = completedResults.length > 0
    ? completedResults.reduce((best, current) => 
        (current.holdProfit || 0) > (best.holdProfit || 0) ? current : best
      )
    : null;

  // Calculate aggregate metrics from stats (using HOLD strategy - realistic performance)
  // Note: We use holdStats instead of highStats because "high" is an unrealistic upper bound
  const resultsWithStats = completedResults.filter(r => r.holdStats);
  const avgWinRatio = resultsWithStats.length > 0
    ? resultsWithStats.reduce((sum, r) => sum + (r.holdStats?.winRatio || 0), 0) / resultsWithStats.length
    : null;
  const avgProfitFactor = resultsWithStats.length > 0
    ? resultsWithStats.reduce((sum, r) => sum + (r.holdStats?.profitFactor || 0), 0) / resultsWithStats.length
    : null;
  const totalTrades = resultsWithStats.reduce((sum, r) => sum + (r.holdStats?.totalTradesTaken || 0), 0);
  const avgMaxDrawdown = resultsWithStats.length > 0
    ? resultsWithStats.reduce((sum, r) => sum + (r.holdStats?.maxDrawdown || 0), 0) / resultsWithStats.length
    : null;

  return (
    <div className="space-y-3">
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        <div className="rounded-xl border border-border/80 bg-card p-3">
          <div className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground mb-1">Total Backtests</div>
          <div className="text-xl font-semibold tabular-nums text-foreground">{totalBacktests}</div>
        </div>
        <div className="rounded-xl border border-border/80 bg-card p-3">
          <div className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground mb-1">Credits Used</div>
          <div className="text-xl font-semibold tabular-nums text-foreground">{totalCreditsUsed.toFixed(0)}</div>
        </div>
        <div className="rounded-xl border border-border/80 bg-card p-3">
          <div className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground mb-1">Completed</div>
          <div className="text-xl font-semibold tabular-nums text-green-600 dark:text-green-400">{completedBacktests}</div>
        </div>
        <div className="rounded-xl border border-border/80 bg-card p-3">
          <div className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground mb-1">In Progress</div>
          <div className="text-xl font-semibold tabular-nums text-yellow-600 dark:text-yellow-400">{inProgressBacktests}</div>
        </div>
      </div>

      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        <div className="rounded-xl border border-border/80 bg-card p-3">
          <div className="flex items-center gap-1.5 mb-1">
            {avgHoldProfit >= 0 ? (
              <TrendingUp className="h-3 w-3 text-green-600 dark:text-green-400" />
            ) : (
              <TrendingDown className="h-3 w-3 text-red-600 dark:text-red-400" />
            )}
            <div className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground">Avg Hold P/L</div>
          </div>
          <div className={`text-lg font-semibold tabular-nums ${
            avgHoldProfit >= 0 
              ? 'text-green-600 dark:text-green-400' 
              : 'text-red-600 dark:text-red-400'
          }`}>
            {formatCurrency(avgHoldProfit)}
          </div>
        </div>

        <div className="rounded-xl border border-border/80 bg-card p-3">
          <div className="flex items-center gap-1.5 mb-1">
            {avgHighProfit >= 0 ? (
              <TrendingUp className="h-3 w-3 text-green-600 dark:text-green-400" />
            ) : (
              <TrendingDown className="h-3 w-3 text-red-600 dark:text-red-400" />
            )}
            <div className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground">Avg High P/L</div>
          </div>
          <div className={`text-lg font-semibold tabular-nums ${
            avgHighProfit >= 0 
              ? 'text-green-600 dark:text-green-400' 
              : 'text-red-600 dark:text-red-400'
          }`}>
            {formatCurrency(avgHighProfit)}
          </div>
        </div>

        <div className="rounded-xl border border-border/80 bg-card p-3">
          <div className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground mb-1">Avg Credits/Test</div>
          <div className="text-lg font-semibold tabular-nums text-foreground">
            {avgCreditsPerBacktest.toFixed(1)}
          </div>
        </div>

        <div className="rounded-xl border border-border/80 bg-card p-3">
          <div className="flex items-center gap-1.5 mb-1">
            {successRate >= 80 ? (
              <CheckCircle2 className="h-3 w-3 text-green-600 dark:text-green-400" />
            ) : successRate >= 50 ? (
              <CheckCircle2 className="h-3 w-3 text-yellow-600 dark:text-yellow-400" />
            ) : (
              <XCircle className="h-3 w-3 text-red-600 dark:text-red-400" />
            )}
            <div className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground">Success Rate</div>
          </div>
          <div className={`text-lg font-semibold tabular-nums ${
            successRate >= 80 
              ? 'text-green-600 dark:text-green-400' 
              : successRate >= 50
              ? 'text-yellow-600 dark:text-yellow-400'
              : 'text-red-600 dark:text-red-400'
          }`}>
            {successRate.toFixed(1)}%
          </div>
        </div>
      </div>

      {/* Aggregate Metrics from Stats (Hold Strategy - Realistic Performance) */}
      {resultsWithStats.length > 0 && (
        <>
          <div className="text-xs text-muted-foreground px-1">
            Note: Metrics below use "Hold" strategy (realistic). "High" strategy represents theoretical upper bound.
          </div>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
            {avgWinRatio !== null && (
              <div className="rounded-xl border border-border/80 bg-card p-3">
                <div className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground mb-1">Avg Win Ratio (Hold)</div>
                <div className={`text-lg font-semibold tabular-nums ${
                  avgWinRatio >= 0.5 
                    ? 'text-green-600 dark:text-green-400' 
                    : avgWinRatio >= 0.4
                    ? 'text-yellow-600 dark:text-yellow-400'
                    : 'text-red-600 dark:text-red-400'
                }`}>
                  {(avgWinRatio * 100).toFixed(1)}%
                </div>
              </div>
            )}

            {avgProfitFactor !== null && (
              <div className="rounded-xl border border-border/80 bg-card p-3">
                <div className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground mb-1">Avg Profit Factor (Hold)</div>
                <div className={`text-lg font-semibold tabular-nums ${
                  avgProfitFactor > 1.5 
                    ? 'text-green-600 dark:text-green-400' 
                    : avgProfitFactor > 1.0
                    ? 'text-yellow-600 dark:text-yellow-400'
                    : 'text-red-600 dark:text-red-400'
                }`}>
                  {avgProfitFactor.toFixed(2)}
                </div>
              </div>
            )}

            {totalTrades > 0 && (
              <div className="rounded-xl border border-border/80 bg-card p-3">
                <div className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground mb-1">Total Trades (Hold)</div>
                <div className="text-lg font-semibold tabular-nums text-foreground">
                  {totalTrades}
                </div>
              </div>
            )}

            {avgMaxDrawdown !== null && (
              <div className="rounded-xl border border-border/80 bg-card p-3">
                <div className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground mb-1">Avg Max Drawdown (Hold)</div>
                <div className={`text-lg font-semibold tabular-nums ${
                  avgMaxDrawdown < 0.1 
                    ? 'text-green-600 dark:text-green-400' 
                    : avgMaxDrawdown < 0.2
                    ? 'text-yellow-600 dark:text-yellow-400'
                    : 'text-red-600 dark:text-red-400'
                }`}>
                  {(avgMaxDrawdown * 100).toFixed(1)}%
                </div>
              </div>
            )}
          </div>
        </>
      )}

      {bestBacktest && (
        <div className="rounded-xl border border-border/80 bg-card p-3">
          <div className="flex items-center gap-2 mb-1">
            <Award className="h-4 w-4 text-amber-600 dark:text-amber-400" />
            <div className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground">Best Performer</div>
          </div>
          <div className="flex items-center justify-between">
            <div className="text-sm text-muted-foreground">
              ID: <span className="font-mono text-foreground">{bestBacktest.id.substring(0, 8)}...</span>
            </div>
            <div className={`text-lg font-semibold tabular-nums ${
              (bestBacktest.holdProfit || 0) >= 0 
                ? 'text-green-600 dark:text-green-400' 
                : 'text-red-600 dark:text-red-400'
            }`}>
              {formatCurrency(bestBacktest.holdProfit || 0)}
            </div>
            {bestBacktest.highProfit && (
              <div className="text-xs text-muted-foreground tabular-nums mt-1">
                High (theoretical): {formatCurrency(bestBacktest.highProfit)}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}; 