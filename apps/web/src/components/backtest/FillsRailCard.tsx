import { Card } from '../ui/card';
import { RailRow } from './BacktestReport';
import type { BacktestFillSettings } from '../../types/backtest';

const ENTRY_FILL_LABELS: Record<BacktestFillSettings['entryFill'], string> = {
  nextBarOpen: 'Next bar open',
  signalClose: 'Signal close',
};

const formatEntryFill = (entryFill: BacktestFillSettings['entryFill']) =>
  ENTRY_FILL_LABELS[entryFill] ?? entryFill;

/**
 * The fill model a backtest ran with. A stored backtest without fill settings predates
 * them: it filled entries at the signal bar's close with no slippage, which overstates
 * results relative to newer runs — say so rather than showing nothing.
 */
export function FillsRailCard({ fillSettings }: { fillSettings?: Partial<BacktestFillSettings> | null }) {
  return (
    <Card className="p-4">
      <h3 className="mb-1 text-[11px] uppercase tracking-widest text-muted-foreground">Fills</h3>
      {fillSettings ? (
        <>
          <RailRow label="Entry fill" value={formatEntryFill(fillSettings.entryFill ?? 'nextBarOpen')} />
          <RailRow label="Slippage" value={`${fillSettings.slippagePercent ?? 0}%`} />
          <RailRow label="Stop slippage" value={`${fillSettings.stopSlippagePercent ?? 0}%`} />
        </>
      ) : (
        <>
          <RailRow label="Entry fill" value="Signal close" />
          <RailRow label="Slippage" value="None" />
          <p className="mt-2 text-xs text-muted-foreground">
            Ran before realistic fills: entries priced at the signal bar&apos;s close with no
            slippage, so results read optimistic. Re-run to compare.
          </p>
        </>
      )}
    </Card>
  );
}
