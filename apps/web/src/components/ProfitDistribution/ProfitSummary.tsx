
import { formatCurrency } from '../../utils/formatters';
import { ProfitData } from './types';
import { calculateProfitStats } from './utils';

interface ProfitSummaryProps {
  data: ProfitData[];
}

export function ProfitSummary({ data }: ProfitSummaryProps) {
  const stats = calculateProfitStats(data);
  
  return (
    <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
      {Object.entries(stats).map(([strategy, { sum, average, count, winRate }]) => (
        <div key={strategy} className="rounded-xl border border-border/80 bg-card p-4">
          <h4 className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground mb-2">{strategy} Strategy</h4>
          <div className="space-y-2">
            <div>
              <p className="text-xs text-muted-foreground">Total Profit</p>
              <p className={`text-lg font-semibold tabular-nums ${sum >= 0 ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'}`}>
                {formatCurrency(sum)}
              </p>
            </div>
            <div>
              <p className="text-xs text-muted-foreground">Average Profit</p>
              <p className={`text-lg font-semibold tabular-nums ${average >= 0 ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'}`}>
                {formatCurrency(average)}
              </p>
            </div>
            <div>
              <p className="text-xs text-muted-foreground">Trade Count</p>
              <p className="text-lg font-semibold tabular-nums text-foreground">{count}</p>
            </div>
            <div>
              <p className="text-xs text-muted-foreground">Win Rate</p>
              <p className="text-lg font-semibold tabular-nums text-foreground">
                {winRate.toFixed(1)}%
              </p>
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}
