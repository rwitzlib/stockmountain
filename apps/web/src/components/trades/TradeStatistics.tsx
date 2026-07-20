import { Trade } from '../../types/trade';

interface TradeStatisticsProps {
  trades: Trade[];
}

export const TradeStatistics = ({ trades }: TradeStatisticsProps) => {
  // Calculate statistics
  const totalTrades = trades.length;
  const openTrades = trades.filter(trade => trade.orderStatus === 'Open').length;
  const totalProfit = trades.reduce((sum, trade) => sum + (trade.profit || 0), 0);

  // Calculate max concurrent trades
  const getMaxConcurrentTrades = () => {
    const timeline: { date: number; isOpen: boolean }[] = [];
    
    trades.forEach(trade => {
      if (trade.openedAt) {
        timeline.push({
          date: new Date(trade.openedAt).getTime(),
          isOpen: true
        });
      }
      if (trade.closedAt && trade.closedAt !== 'null') {
        timeline.push({
          date: new Date(trade.closedAt).getTime(),
          isOpen: false
        });
      }
    });

    timeline.sort((a, b) => a.date - b.date);

    let currentConcurrent = 0;
    let maxConcurrent = 0;

    timeline.forEach(event => {
      if (event.isOpen) {
        currentConcurrent++;
        maxConcurrent = Math.max(maxConcurrent, currentConcurrent);
      } else {
        currentConcurrent--;
      }
    });

    return maxConcurrent;
  };

  const maxConcurrentTrades = getMaxConcurrentTrades();

  return (
    <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
      <div className="p-3 rounded-xl border border-border/80 bg-card">
        <div className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground mb-1">Total Trades</div>
        <div className="text-xl font-semibold tabular-nums text-foreground">{totalTrades}</div>
      </div>
      <div className="p-3 rounded-xl border border-border/80 bg-card">
        <div className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground mb-1">Open Positions</div>
        <div className="text-xl font-semibold tabular-nums text-foreground">{openTrades}</div>
      </div>
      <div className="p-3 rounded-xl border border-border/80 bg-card">
        <div className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground mb-1">Max Concurrent</div>
        <div className="text-xl font-semibold tabular-nums text-foreground">{maxConcurrentTrades}</div>
      </div>
      <div className="p-3 rounded-xl border border-border/80 bg-card">
        <div className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground mb-1">Total P/L</div>
        <div className={`text-xl font-semibold tabular-nums ${
          totalProfit >= 0 
            ? 'text-green-600 dark:text-green-400' 
            : 'text-red-600 dark:text-red-400'
        }`}>
          ${totalProfit.toFixed(2)}
        </div>
      </div>
    </div>
  );
};