
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
    { name: 'Hold', color: 'var(--chart-strategy)', trades: payload[0]?.payload.holdTrades },
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
    <div className="rounded-lg border border-border bg-popover p-4 shadow-md">
      <p className="text-xs font-semibold text-foreground mb-3">{formatDateTime(payload[0]?.payload.rawDate)}</p>
      {strategies.map(({ name, color, trades }) => (
        <div key={name} className="mb-3 last:mb-0">
          <div className="flex items-center gap-2 mb-2">
            <div className="w-2.5 h-2.5 rounded-full" style={{ backgroundColor: color }} />
            <span className="text-xs text-muted-foreground">
              {name}: <span className="font-semibold text-foreground tabular-nums">{formatCurrency(payload.find(p => p.name === name)?.value)}</span>
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
    <div className="text-xs space-y-1.5">
      {trades.bought.length > 0 && (
        <div className="rounded-md bg-green-500/10 p-2">
          <p className="text-[10px] font-medium uppercase tracking-widest text-green-600 dark:text-green-400 mb-1">Bought</p>
          {trades.bought.map((trade, i) => (
            <p key={i} className="text-muted-foreground ml-2 text-[10px] tabular-nums">
              {trade.shares} <span className="font-mono">{trade.ticker}</span> @ {formatCurrency(trade.price)}
            </p>
          ))}
        </div>
      )}
      {trades.sold.length > 0 && (
        <div className="rounded-md bg-red-500/10 p-2">
          <p className="text-[10px] font-medium uppercase tracking-widest text-red-600 dark:text-red-400 mb-1">Sold</p>
          {trades.sold.map((trade, i) => (
            <p key={i} className="text-muted-foreground ml-2 text-[10px] tabular-nums">
              {trade.shares} <span className="font-mono">{trade.ticker}</span> @ {formatCurrency(trade.price)} 
              {' '}({trade.stoppedOut ? 'stopped' : `${formatCurrency(trade.profit)} profit`})
            </p>
          ))}
        </div>
      )}
    </div>
  );
}
