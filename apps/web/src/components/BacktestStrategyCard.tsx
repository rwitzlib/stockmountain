import { ArrowDownRight, ArrowUpRight, Minus, Users } from 'lucide-react';
import { TradeStrategy } from '../types/types';
import { Card } from './ui/card';

interface BacktestStrategyCardProps {
  title: string;
  strategy: TradeStrategy;
}

export function BacktestStrategyCard({ title, strategy }: BacktestStrategyCardProps) {
  const profit = strategy.sumProfit ?? strategy.balanceChange ?? 0;
  const profitColor = profit > 0
    ? 'text-green-600 dark:text-green-400'
    : profit < 0
    ? 'text-red-600 dark:text-red-400'
    : 'text-muted-foreground';
  const ProfitIcon = profit > 0 ? ArrowUpRight : profit < 0 ? ArrowDownRight : Minus;

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
            ${Math.abs(profit).toFixed(2)}
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
          <span className="text-sm font-mono font-bold text-primary dark:text-cyan-400">{strategy.maxConcurrentPositions}</span>
        </div>
        <div className="flex items-center gap-1.5">
          <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">TOTAL TRADES</span>
          <span className="text-sm font-mono font-bold text-primary dark:text-cyan-400">{strategy.totalTradesTaken ?? 0}</span>
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
            <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">DAILY STD DEV</span>
            <span className="text-sm font-mono font-bold text-primary dark:text-cyan-400">
              {(strategy.dailyReturnStdDev * 100).toFixed(3)}%
            </span>
          </div>
        )}
        {strategy.sharpeRatio != null && (
          <div className="flex items-center gap-1.5">
            <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">SHARPE</span>
            <span className="text-sm font-mono font-bold text-primary dark:text-cyan-400">
              {strategy.sharpeRatio.toFixed(2)}
            </span>
          </div>
        )}
        {strategy.maxDrawdown != null && (
          <div className="flex items-center gap-1.5">
            <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">MAX DRAWDOWN</span>
            <span className="text-sm font-mono font-bold text-primary dark:text-cyan-400">
              {(strategy.maxDrawdown * 100).toFixed(2)}%
            </span>
          </div>
        )}
        {strategy.profitFactor != null && Number.isFinite(strategy.profitFactor) && (
          <div className="flex items-center gap-1.5">
            <span className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">PROFIT FACTOR</span>
            <span className="text-sm font-mono font-bold text-primary dark:text-cyan-400">
              {strategy.profitFactor.toFixed(2)}
            </span>
          </div>
        )}
      </div>
    </Card>
  );
}
