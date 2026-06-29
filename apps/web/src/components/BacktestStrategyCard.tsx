import { ArrowDownRight, ArrowUpRight, Minus, Users } from 'lucide-react';
import { TradeStrategy, TradingData, DailyResult } from '../types/types';
import { Card } from './ui/card';

interface BacktestStrategyCardProps {
  title: string;
  strategy: TradeStrategy;
  tradingData: TradingData;
  strategyKey: 'hold' | 'high' | 'other';
}

export function BacktestStrategyCard({ title, strategy, tradingData, strategyKey }: BacktestStrategyCardProps) {
  const profitColor = strategy.sumProfit > 0 
    ? 'text-green-600 dark:text-green-400' 
    : strategy.sumProfit < 0 
    ? 'text-red-600 dark:text-red-400' 
    : 'text-muted-foreground';
  const ProfitIcon = strategy.sumProfit > 0 ? ArrowUpRight : strategy.sumProfit < 0 ? ArrowDownRight : Minus;

  // Calculate max concurrent positions from trading data
  const calculateMaxConcurrentPositions = (): number => {
    const positionEvents: { timestamp: number; type: 'open' | 'close'; ticker: string }[] = [];

    // Collect all buy and sell events for this strategy
    tradingData.results.forEach((dailyResult: DailyResult) => {
      const strategyResult = dailyResult[strategyKey];
      if (!strategyResult) return;

      // Add buy events (position opens)
      strategyResult.bought.forEach(trade => {
        positionEvents.push({
          timestamp: new Date(trade.timestamp).getTime(),
          type: 'open',
          ticker: trade.ticker
        });
      });

      // Add sell events (position closes)
      strategyResult.sold.forEach(trade => {
        positionEvents.push({
          timestamp: new Date(trade.timestamp).getTime(),
          type: 'close',
          ticker: trade.ticker
        });
      });
    });

    // Sort events by timestamp
    positionEvents.sort((a, b) => a.timestamp - b.timestamp);

    // Track concurrent positions
    let currentPositions = 0;
    let maxConcurrent = 0;
    const openPositions = new Set<string>();

    positionEvents.forEach(event => {
      if (event.type === 'open') {
        openPositions.add(event.ticker);
        currentPositions = openPositions.size;
        maxConcurrent = Math.max(maxConcurrent, currentPositions);
      } else if (event.type === 'close') {
        openPositions.delete(event.ticker);
        currentPositions = openPositions.size;
      }
    });

    return maxConcurrent;
  };

  // Use maxConcurrentPositions from strategy if available, otherwise calculate it
  const maxConcurrentPositions = strategy.maxConcurrentPositions > 0 
    ? strategy.maxConcurrentPositions 
    : calculateMaxConcurrentPositions();
  
  // Use totalTradesTaken from strategy if available, otherwise calculate from tradingData
  const totalTradesTaken = strategy.totalTradesTaken ?? 0;

  return (
    <Card className="bg-card/50 border border-border p-3 hover:border-primary dark:hover:border-cyan-700 transition-colors">
      <div className="flex items-center gap-2 mb-3">
        <div className="flex items-center gap-2 px-2.5 py-1 bg-primary/10 dark:bg-cyan-950/50 border border-primary/30 dark:border-cyan-800/50 rounded-md">
          <h3 className="text-xs font-mono font-bold uppercase tracking-wider text-primary dark:text-cyan-400">{title}</h3>
        </div>
      </div>
      <div className="space-y-2">
        <div className="flex items-center gap-1.5">
          <ProfitIcon className={`w-3.5 h-3.5 ${profitColor}`} />
          <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">TOTAL PROFIT</span>
          <span className={`text-sm font-mono font-bold ${profitColor}`}>
            ${Math.abs(strategy.sumProfit).toFixed(2)}
          </span>
        </div>
        <div className="flex items-center gap-1.5">
          <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">WIN RATE</span>
          <span className="text-sm font-mono font-bold text-primary dark:text-cyan-400">
            {(strategy.winRatio * 100).toFixed(1)}%
          </span>
        </div>
        <div className="flex items-center gap-1.5">
          <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">AVG WIN</span>
          <span className="text-sm font-mono font-bold text-primary dark:text-cyan-400">${strategy.avgWin.toFixed(2)}</span>
        </div>
        <div className="flex items-center gap-1.5">
          <Users className="w-3.5 h-3.5 text-primary dark:text-cyan-400" />
          <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">MAX CONCURRENT</span>
          <span className="text-sm font-mono font-bold text-primary dark:text-cyan-400">{maxConcurrentPositions}</span>
        </div>
        <div className="flex items-center gap-1.5">
          <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">TOTAL TRADES</span>
          <span className="text-sm font-mono font-bold text-primary dark:text-cyan-400">{totalTradesTaken}</span>
        </div>
        {strategy.averageDailyReturn != null && (
          <div className="flex items-center gap-1.5">
            <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">AVG DAILY RETURN</span>
            <span className="text-sm font-mono font-bold text-primary dark:text-cyan-400">
              {(strategy.averageDailyReturn * 100).toFixed(3)}%
            </span>
          </div>
        )}
        {strategy.dailyReturnStdDev != null && (
          <div className="flex items-center gap-1.5">
            <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">DAILY RETURN STD</span>
            <span className="text-sm font-mono font-bold text-primary dark:text-cyan-400">
              {(strategy.dailyReturnStdDev * 100).toFixed(3)}%
            </span>
          </div>
        )}
        {strategy.sharpeRatio != null && (
          <div className="flex items-center gap-1.5">
            <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">SHARPE RATIO</span>
            <span className="text-sm font-mono font-bold text-primary dark:text-cyan-400">
              {Number(strategy.sharpeRatio).toFixed(3)}
            </span>
          </div>
        )}
        {strategy.maxDrawdown != null && (
          <div className="flex items-center gap-1.5">
            <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">MAX DRAWDOWN</span>
            <span className="text-sm font-mono font-bold text-red-600 dark:text-red-400">
              {(strategy.maxDrawdown * 100).toFixed(2)}%
            </span>
          </div>
        )}
        {strategy.profitFactor != null && (
          <div className="flex items-center gap-1.5">
            <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">PROFIT FACTOR</span>
            <span className="text-sm font-mono font-bold text-primary dark:text-cyan-400">
              {Number(strategy.profitFactor).toFixed(3)}
            </span>
          </div>
        )}
      </div>
    </Card>
  );
}
