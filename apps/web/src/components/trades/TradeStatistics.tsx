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
      <div className="p-3 border border-border bg-card hover:border-primary dark:hover:border-cyan-700 transition-colors">
        <div className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground mb-1">:: Total Trades</div>
        <div className="text-xl font-mono font-bold text-primary dark:text-cyan-400">{totalTrades}</div>
      </div>
      <div className="p-3 border border-border bg-card hover:border-green-500 dark:hover:border-green-700 transition-colors">
        <div className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground mb-1">:: Open Positions</div>
        <div className="text-xl font-mono font-bold text-green-600 dark:text-green-400">{openTrades}</div>
      </div>
      <div className="p-3 border border-border bg-card hover:border-yellow-500 dark:hover:border-yellow-700 transition-colors">
        <div className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground mb-1">:: Max Concurrent</div>
        <div className="text-xl font-mono font-bold text-yellow-600 dark:text-yellow-400">{maxConcurrentTrades}</div>
      </div>
      <div className="p-3 border border-border bg-card hover:border-primary dark:hover:border-cyan-700 transition-colors">
        <div className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground mb-1">:: Total P/L</div>
        <div className={`text-xl font-mono font-bold ${
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