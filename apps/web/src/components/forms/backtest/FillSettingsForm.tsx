import { NumberInput } from '../../ui/NumberInput';
import type { BacktestEntryFill, BacktestFillSettings } from '../../../types/backtest';

interface FillSettingsFormProps {
  value: BacktestFillSettings;
  onChange: (value: BacktestFillSettings) => void;
}

const ENTRY_FILLS: { value: BacktestEntryFill; label: string; hint: string }[] = [
  {
    value: 'nextBarOpen',
    label: 'Next bar open',
    hint: 'The first price that can trade once the signal bar has completed.',
  },
  {
    value: 'signalClose',
    label: 'Signal close',
    hint: 'Optimistic: that price is gone by the time the signal is known. For parity studies against paper trading.',
  },
];

// Mirrors BacktestFillSettings on the API: omitted fill settings run with these.
export const defaultFillSettings: BacktestFillSettings = {
  entryFill: 'nextBarOpen',
  slippagePercent: 0,
  stopSlippagePercent: 0.5,
};

export const MAX_SLIPPAGE_PERCENT = 10;

function FieldLabel({ label, hint }: { label: string; hint: string }) {
  return (
    <div
      className="mb-2 text-[11px] font-medium uppercase tracking-widest text-muted-foreground"
      title={hint}
    >
      {label}
    </div>
  );
}

export function FillSettingsForm({ value, onChange }: FillSettingsFormProps) {
  return (
    <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
      <div className="rounded-lg border border-border/60 p-3">
        <FieldLabel label="Entry fill" hint="The price an entry is booked at" />
        <div className="flex h-10 overflow-hidden rounded-lg border border-input">
          {ENTRY_FILLS.map((option) => (
            <button
              key={option.value}
              type="button"
              title={option.hint}
              onClick={() => onChange({ ...value, entryFill: option.value })}
              className={`flex-1 px-2 text-sm transition-colors ${
                value.entryFill === option.value
                  ? 'bg-accent font-semibold text-foreground'
                  : 'bg-card text-muted-foreground hover:bg-accent/50'
              }`}
            >
              {option.label}
            </button>
          ))}
        </div>
      </div>

      <div className="rounded-lg border border-border/60 p-3">
        <FieldLabel
          label="Slippage"
          hint="Applied against you on market fills: the entry and the timed exit. Take-profit fills never slip."
        />
        <NumberInput
          value={value.slippagePercent}
          onChange={(next) => onChange({ ...value, slippagePercent: next ?? 0 })}
          min={0}
          max={MAX_SLIPPAGE_PERCENT}
          step={0.05}
          suffix="%"
          defaultValue={0}
          required
        />
      </div>

      <div className="rounded-lg border border-border/60 p-3">
        <FieldLabel
          label="Stop slippage"
          hint="Applied on top of the stop (or gap-through) price. Stops sell into a falling bid, so they slip the most — thin, low-priced names more than liquid ones."
        />
        <NumberInput
          value={value.stopSlippagePercent}
          onChange={(next) => onChange({ ...value, stopSlippagePercent: next ?? 0 })}
          min={0}
          max={MAX_SLIPPAGE_PERCENT}
          step={0.1}
          suffix="%"
          defaultValue={0.5}
          required
        />
      </div>
    </div>
  );
}
