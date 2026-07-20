import { useNavigate } from 'react-router-dom';
import { Trade } from '../../types/trade';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../ui/table';
import { ArrowUpDown } from 'lucide-react';
import { formatPrice, formatProfit } from '../../utils/chartUtils';

interface TradesTableProps {
  trades: Trade[];
  sortConfig: {
    key: keyof Trade | null;
    direction: 'asc' | 'desc';
  };
  onSort: (key: keyof Trade) => void;
  compact?: boolean;
}

export const TradesTable = ({ trades, sortConfig, onSort, compact = false }: TradesTableProps) => {
  const navigate = useNavigate();

  const formatDate = (dateString: string | null) => {
    if (!dateString || dateString === 'null') return '';
    if (compact) {
      return new Date(dateString).toLocaleDateString('en-US', {
        month: 'numeric',
        day: 'numeric'
      });
    }
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  };

  const handleRowClick = (trade: Trade) => {
    navigate(`/optimus/trade/${trade.id}`, { state: { trade } });
  };

  // Compact view shows fewer columns
  if (compact) {
    return (
      <div className="table-container rounded-xl border border-border/80 bg-card">
        <Table>
          <TableHeader>
            <TableRow className="border-border hover:bg-muted/50">
              <TableHead 
                className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground cursor-pointer hover:text-foreground transition-colors py-1.5 px-2"
                onClick={() => onSort('ticker')}
              >
                Ticker
              </TableHead>
              <TableHead 
                className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground cursor-pointer hover:text-foreground transition-colors py-1.5 px-2"
                onClick={() => onSort('profit')}
              >
                P/L
              </TableHead>
              <TableHead 
                className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground cursor-pointer hover:text-foreground transition-colors py-1.5 px-2"
                onClick={() => onSort('closedAt')}
              >
                Date
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {trades.map((trade) => (
              <TableRow 
                key={trade.id}
                className="border-border hover:bg-accent cursor-pointer transition-colors"
                onClick={() => handleRowClick(trade)}
              >
                <TableCell className="font-mono font-bold text-[10px] text-foreground py-1.5 px-2">{trade.ticker || ''}</TableCell>
                <TableCell className={`font-mono text-[10px] font-bold py-1.5 px-2 ${
                  trade.profit >= 0 
                    ? 'text-green-600 dark:text-green-400' 
                    : 'text-red-600 dark:text-red-400'
                }`}>
                  {trade.profit !== null ? formatProfit(trade.profit) : ''}
                </TableCell>
                <TableCell className="font-mono text-[10px] text-muted-foreground py-1.5 px-2">{formatDate(trade.closedAt)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    );
  }

  return (
    <div className="table-container rounded-xl border border-border/80 bg-card">
      <Table>
        <TableHeader>
          <TableRow className="border-border hover:bg-muted/50">
            <TableHead 
              className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground cursor-pointer hover:text-foreground transition-colors"
              onClick={() => onSort('ticker')}
            >
              Ticker
              <ArrowUpDown className="ml-1 h-3 w-3 inline" />
            </TableHead>
            <TableHead 
              className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground cursor-pointer hover:text-foreground transition-colors"
              onClick={() => onSort('orderStatus')}
            >
              Status
              <ArrowUpDown className="ml-1 h-3 w-3 inline" />
            </TableHead>
            <TableHead 
              className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground cursor-pointer hover:text-foreground transition-colors"
              onClick={() => onSort('profit')}
            >
              P/L
              <ArrowUpDown className="ml-1 h-3 w-3 inline" />
            </TableHead>
            <TableHead 
              className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground cursor-pointer hover:text-foreground transition-colors"
              onClick={() => onSort('entryPrice')}
            >
              Entry
              <ArrowUpDown className="ml-1 h-3 w-3 inline" />
            </TableHead>
            <TableHead 
              className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground cursor-pointer hover:text-foreground transition-colors"
              onClick={() => onSort('closePrice')}
            >
              Exit
              <ArrowUpDown className="ml-1 h-3 w-3 inline" />
            </TableHead>
            <TableHead 
              className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground cursor-pointer hover:text-foreground transition-colors"
              onClick={() => onSort('openedAt')}
            >
              Opened
              <ArrowUpDown className="ml-1 h-3 w-3 inline" />
            </TableHead>
            <TableHead 
              className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground cursor-pointer hover:text-foreground transition-colors"
              onClick={() => onSort('closedAt')}
            >
              Closed
              <ArrowUpDown className="ml-1 h-3 w-3 inline" />
            </TableHead>
            <TableHead 
              className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground cursor-pointer hover:text-foreground transition-colors"
              onClick={() => onSort('shares')}
            >
              Qty
              <ArrowUpDown className="ml-1 h-3 w-3 inline" />
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {trades.map((trade) => (
            <TableRow 
              key={trade.id}
              className="border-border hover:bg-accent cursor-pointer transition-colors"
              onClick={() => handleRowClick(trade)}
            >
              <TableCell className="font-mono font-bold text-xs text-foreground">{trade.ticker || ''}</TableCell>
              <TableCell>
                {trade.orderStatus && (
                  <span className="rounded-full border border-border px-2 py-0.5 text-[10px] font-medium uppercase tracking-wider text-muted-foreground">
                    {trade.orderStatus}
                  </span>
                )}
              </TableCell>
              <TableCell className={`font-mono text-xs font-bold ${
                trade.profit >= 0 
                  ? 'text-green-600 dark:text-green-400' 
                  : 'text-red-600 dark:text-red-400'
              }`}>
                {trade.profit !== null ? formatProfit(trade.profit) : ''}
              </TableCell>
              <TableCell className="font-mono text-xs tabular-nums text-foreground">{trade.entryPrice ? formatPrice(trade.entryPrice) : ''}</TableCell>
              <TableCell className="font-mono text-xs tabular-nums text-foreground">{trade.closePrice ? formatPrice(trade.closePrice) : ''}</TableCell>
              <TableCell className="font-mono text-xs text-muted-foreground">{formatDate(trade.openedAt)}</TableCell>
              <TableCell className="font-mono text-xs text-muted-foreground">{formatDate(trade.closedAt)}</TableCell>
              <TableCell className="font-mono text-xs tabular-nums text-foreground">{trade.shares || ''}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
};