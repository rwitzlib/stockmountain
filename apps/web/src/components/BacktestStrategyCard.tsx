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
    <Card className="p-3">
      <div className="flex items-center gap-2 mb-3">
        <h3 className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">{title}</h3>
      </div>
      <div className="space-y-2">
        <div className="flex items-center gap-1.5">
          <ProfitIcon className={`w-3.5 h-3.5 ${profitColor}`} />
          <span className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground">Total Profit</span>
          <span className={`text-sm font-semibold tabular-nums ${profitColor}`}>
            ${Math.abs(profit).toFixed(2)}
          </span>
        </div>
        <div className="flex items-center gap-1.5">
          <span className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground">Win Rate</span>
          <span className="text-sm font-semibold tabular-nums text-foreground">
            {(strategy.winRatio * 100).toFixed(1)}%
          </span>
        </div>
        <div className="flex items-center gap-1.5">
          <span className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground">Avg Win</span>
          <span className="text-sm font-semibold tabular-nums text-foreground">${strategy.avgWin.toFixed(2)}</span>
        </div>
        <div className="flex items-center gap-1.5">
          <Users className="w-3.5 h-3.5 text-muted-foreground" />
          <span className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground">Max Concurrent</span>
          <span className="text-sm font-semibold tabular-nums text-foreground">{strategy.maxConcurrentPositions}</span>
        </div>
        <div className="flex items-center gap-1.5">
          <span className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground">Total Trades</span>
          <span className="text-sm font-semibold tabular-nums text-foreground">{strategy.totalTradesTaken ?? 0}</span>
        </div>
        {strategy.averageDailyReturn != null && (
          <div className="flex items-center gap-1.5">
            <span className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground">Avg Daily Return</span>
            <span className="text-sm font-semibold tabular-nums text-foreground">
              {(strategy.averageDailyReturn * 100).toFixed(3)}%
            </span>
          </div>
        )}
        {strategy.dailyReturnStdDev != null && (
          <div className="flex items-center gap-1.5">
            <span className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground">Daily Std Dev</span>
            <span className="text-sm font-semibold tabular-nums text-foreground">
              {(strategy.dailyReturnStdDev * 100).toFixed(3)}%
            </span>
          </div>
        )}
        {strategy.sharpeRatio != null && (
          <div className="flex items-center gap-1.5">
            <span className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground">Sharpe</span>
            <span className="text-sm font-semibold tabular-nums text-foreground">
              {strategy.sharpeRatio.toFixed(2)}
            </span>
          </div>
        )}
        {strategy.maxDrawdown != null && (
          <div className="flex items-center gap-1.5">
            <span className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground">Max Drawdown</span>
            <span className="text-sm font-semibold tabular-nums text-foreground">
              {(strategy.maxDrawdown * 100).toFixed(2)}%
            </span>
          </div>
        )}
        {strategy.profitFactor != null && Number.isFinite(strategy.profitFactor) && (
          <div className="flex items-center gap-1.5">
            <span className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground">Profit Factor</span>
            <span className="text-sm font-semibold tabular-nums text-foreground">
              {strategy.profitFactor.toFixed(2)}
            </span>
          </div>
        )}
      </div>
    </Card>
  );
}
