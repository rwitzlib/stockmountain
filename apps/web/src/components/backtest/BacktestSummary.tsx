import { BacktestEntry } from '../../types/backtest';
import { Loader2 } from 'lucide-react';

interface BacktestSummaryProps {
  results: BacktestEntry[];
  credits: number | null;
  maxCredits: number | null;
}

export const BacktestSummary = ({ results, credits, maxCredits }: BacktestSummaryProps) => {
  const totalBacktests = results.length;
  const inProgress = results.filter(r => r.status === 'InProgress').length;

  const hasAllowance = credits !== null && maxCredits !== null && maxCredits > 0;
  const used = hasAllowance ? Math.min(Math.max(maxCredits - credits, 0), maxCredits) : 0;
  const usedPct = hasAllowance ? (used / maxCredits) * 100 : 0;
  const meterColor =
    usedPct >= 95
      ? 'bg-red-600 dark:bg-red-500'
      : usedPct >= 80
      ? 'bg-amber-500'
      : 'bg-primary';

  return (
    <div className="flex justify-end">
      <div className="flex w-full sm:w-auto items-stretch rounded-xl border border-border/80 bg-card divide-x divide-border/80">
        <div className="flex-1 sm:flex-none px-4 py-2.5">
          <div className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground mb-0.5">Backtests</div>
          <div className="text-lg font-semibold tabular-nums leading-tight text-foreground">{totalBacktests}</div>
        </div>
        <div className="flex-1 sm:flex-none px-4 py-2.5">
          <div className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground mb-0.5">In Progress</div>
          <div
            className={`flex items-center gap-1.5 text-lg font-semibold tabular-nums leading-tight ${
              inProgress > 0 ? 'text-yellow-600 dark:text-yellow-400' : 'text-foreground'
            }`}
          >
            {inProgress > 0 && <Loader2 className="h-3.5 w-3.5 animate-spin" />}
            {inProgress}
          </div>
        </div>
        <div className="flex-1 sm:flex-none px-4 py-2.5 sm:min-w-[200px]">
          <div className="flex items-baseline justify-between gap-3 mb-0.5">
            <div className="text-[10.5px] font-medium uppercase tracking-widest text-muted-foreground">Credits</div>
            <div className="text-xs font-semibold tabular-nums text-foreground">
              {credits === null
                ? '—'
                : hasAllowance
                ? `${Math.round(used)} / ${Math.round(maxCredits)}`
                : `${Math.round(credits)} left`}
            </div>
          </div>
          {hasAllowance && (
            <div
              role="progressbar"
              aria-label="Monthly credits used"
              aria-valuemin={0}
              aria-valuemax={Math.round(maxCredits)}
              aria-valuenow={Math.round(used)}
              className="mt-1.5 h-1.5 w-full rounded-full bg-muted overflow-hidden"
            >
              <div
                className={`h-full rounded-full transition-all ${meterColor}`}
                style={{ width: `${Math.min(usedPct, 100)}%` }}
              />
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
