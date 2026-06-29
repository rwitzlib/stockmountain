
import { Trade } from '../types/types';
import { formatCurrency } from '../utils/formatters';
import { formatDateTime } from '../utils/dateFormatter';

interface TooltipProps {
  active?: boolean;
  payload?: any[];
  label?: string;
}

interface TradeInfo {
  bought: Trade[];
  sold: Trade[];
}

export function CustomTooltip({ active, payload }: TooltipProps) {
  if (!active || !payload) return null;

  const strategies = [
    { name: 'Hold', color: 'hsl(var(--primary))', trades: payload[0]?.payload.holdTrades },
    { name: 'High', color: '#16a34a', trades: payload[1]?.payload.highTrades }
  ];

  // Add Other strategy if it exists in the data
  if (payload[0]?.payload.otherTrades) {
    strategies.push({
      name: 'Other', 
      color: '#d97706', 
      trades: payload[0]?.payload.otherTrades
    });
  }

  return (
    <div className="bg-card border border-border p-4 rounded-lg shadow-lg">
      <p className="text-xs font-mono uppercase tracking-wider text-primary mb-3">{formatDateTime(payload[0]?.payload.rawDate)}</p>
      {strategies.map(({ name, color, trades }) => (
        <div key={name} className="mb-3 last:mb-0">
          <div className="flex items-center gap-2 mb-2">
            <div className="w-2.5 h-2.5 rounded-full border border-border" style={{ backgroundColor: color }} />
            <span className="text-xs font-mono uppercase tracking-wide text-foreground">
              {name}: <span className="text-primary">{formatCurrency(payload.find(p => p.name === name)?.value)}</span>
            </span>
          </div>
          <TradeList trades={trades} />
        </div>
      ))}
    </div>
  );
}

function TradeList({ trades }: { trades: TradeInfo }) {
  if (!trades?.bought?.length && !trades?.sold?.length) return null;

  return (
    <div className="text-xs font-mono space-y-1.5">
      {trades.bought.length > 0 && (
        <div className="bg-green-500/10 border border-green-500/30 p-2 rounded">
          <p className="text-[10px] uppercase tracking-wider text-green-600 dark:text-green-400 mb-1">{'>> '}BOUGHT</p>
          {trades.bought.map((trade, i) => (
            <p key={i} className="text-muted-foreground ml-2 text-[10px]">
              {trade.shares} {trade.ticker} @ {formatCurrency(trade.price)}
            </p>
          ))}
        </div>
      )}
      {trades.sold.length > 0 && (
        <div className="bg-red-500/10 border border-red-500/30 p-2 rounded">
          <p className="text-[10px] uppercase tracking-wider text-red-600 dark:text-red-400 mb-1">{'>> '}SOLD</p>
          {trades.sold.map((trade, i) => (
            <p key={i} className="text-muted-foreground ml-2 text-[10px]">
              {trade.shares} {trade.ticker} @ {formatCurrency(trade.price)} 
              {' '}({trade.stoppedOut ? 'STOPPED' : `${formatCurrency(trade.profit)} PROFIT`})
            </p>
          ))}
        </div>
      )}
    </div>
  );
}
