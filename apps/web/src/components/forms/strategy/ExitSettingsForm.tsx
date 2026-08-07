import { Switch } from '../../ui/switch';
import { NumberInput } from '../../ui/NumberInput';
import type {
  ExitSettings,
  Exit,
  TimedExit,
  ExitValueType,
  Timespan
} from '../../../types/strategy';

interface ExitSettingsFormProps {
  value: ExitSettings;
  onChange: (value: ExitSettings) => void;
}

const VALUE_TYPES: { value: ExitValueType; label: string }[] = [
  { value: 'percent', label: '%' },
  { value: 'flat', label: '$' },
];

const TIMESPAN_OPTIONS: { value: Timespan; label: string }[] = [
  { value: 'minute', label: 'Minutes' },
  { value: 'hour', label: 'Hours' },
  { value: 'day', label: 'Days' },
];

// Stop loss, take profit, and timed exit are mandatory — the API rejects a strategy or
// backtest without all three, so the form always renders them configured.
export const defaultExitSettings: ExitSettings = {
  stopLoss: { type: 'percent', value: -5 },
  takeProfit: { type: 'percent', value: 10 },
  timedExit: { avoidOvernight: true, timeframe: { multiplier: 30, timespan: 'minute' } },
};

function CardLabel({ label, hint, colorClass }: { label: string; hint: string; colorClass: string }) {
  return (
    <div
      className={`mb-2 text-[11px] font-medium uppercase tracking-widest ${colorClass}`}
      title={hint}
    >
      {label}
    </div>
  );
}

function UnitToggle({
  value,
  onChange,
}: {
  value: ExitValueType;
  onChange: (type: ExitValueType) => void;
}) {
  return (
    <div className="flex h-10 shrink-0 overflow-hidden rounded-lg border border-input">
      {VALUE_TYPES.map((vt) => (
        <button
          key={vt.value}
          type="button"
          onClick={() => onChange(vt.value)}
          className={`w-9 text-sm transition-colors ${
            value === vt.value
              ? 'bg-accent font-semibold text-foreground'
              : 'bg-card text-muted-foreground hover:bg-accent/50'
          }`}
        >
          {vt.label}
        </button>
      ))}
    </div>
  );
}

function ExitCard({
  value,
  onChange,
  label,
  hint,
  colorClass,
  defaultValue,
  isProfit = false,
}: {
  value: Exit;
  onChange: (exit: Exit) => void;
  label: string;
  hint: string;
  colorClass: string;
  defaultValue: number;
  isProfit?: boolean;
}) {
  return (
    <div className="rounded-lg border border-border/60 p-3">
      <CardLabel label={label} hint={hint} colorClass={colorClass} />
      <div className="flex gap-2">
        <div className="flex-1">
          <NumberInput
            value={Math.abs(value.value)}
            onChange={(newValue) =>
              onChange({
                ...value,
                value: isProfit ? Math.abs(newValue || defaultValue) : -Math.abs(newValue || defaultValue),
              })
            }
            min={0.1}
            step={value.type === 'percent' ? 0.5 : 1}
            defaultValue={Math.abs(defaultValue)}
            required
          />
        </div>
        <UnitToggle value={value.type} onChange={(type) => onChange({ ...value, type })} />
      </div>
    </div>
  );
}

function TimedExitCard({
  value,
  onChange,
}: {
  value: TimedExit;
  onChange: (timedExit: TimedExit) => void;
}) {
  return (
    <div className="rounded-lg border border-border/60 p-3">
      <CardLabel
        label="Timed Exit"
        hint="Exit after a maximum hold duration or before market close"
        colorClass="text-amber-600 dark:text-amber-400"
      />
      <div className="flex gap-2">
        <div className="w-20 shrink-0">
          <NumberInput
            value={value.timeframe.multiplier}
            onChange={(newValue) =>
              onChange({
                ...value,
                timeframe: { ...value.timeframe, multiplier: newValue || 30 },
              })
            }
            min={1}
            max={999}
            step={1}
            defaultValue={30}
            required
          />
        </div>
        <select
          value={value.timeframe.timespan}
          onChange={(e) =>
            onChange({
              ...value,
              timeframe: { ...value.timeframe, timespan: e.target.value as Timespan },
            })
          }
          className="h-10 flex-1 rounded-lg border border-input bg-card px-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
        >
          {TIMESPAN_OPTIONS.map((opt) => (
            <option key={opt.value} value={opt.value}>{opt.label}</option>
          ))}
        </select>
      </div>
      <div className="mt-2.5 flex items-center justify-between gap-2">
        <span className="text-xs text-muted-foreground" title="Exit before market close instead of holding overnight">
          Avoid overnight
        </span>
        <Switch
          checked={value.avoidOvernight}
          onCheckedChange={(checked) => onChange({ ...value, avoidOvernight: checked })}
        />
      </div>
    </div>
  );
}

export function ExitSettingsForm({ value, onChange }: ExitSettingsFormProps) {
  return (
    <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
      <ExitCard
        value={value.stopLoss}
        onChange={(stopLoss) => onChange({ ...value, stopLoss })}
        label="Stop Loss"
        hint="Exit position to limit losses"
        colorClass="text-red-600 dark:text-red-400"
        defaultValue={-5}
        isProfit={false}
      />

      <ExitCard
        value={value.takeProfit}
        onChange={(takeProfit) => onChange({ ...value, takeProfit })}
        label="Take Profit"
        hint="Take profits when the target is reached"
        colorClass="text-green-600 dark:text-green-400"
        defaultValue={10}
        isProfit={true}
      />

      <TimedExitCard
        value={value.timedExit}
        onChange={(timedExit) => onChange({ ...value, timedExit })}
      />
    </div>
  );
}
