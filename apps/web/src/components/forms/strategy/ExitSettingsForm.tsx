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

function ExitForm({
  value,
  onChange,
  label,
  defaultValue,
  isProfit = false
}: {
  value: Exit;
  onChange: (exit: Exit) => void;
  label: string;
  defaultValue: number;
  isProfit?: boolean;
}) {
  return (
    <div className="space-y-3">
      <div>
        <label className="block text-sm font-medium text-foreground">{label}</label>
        <p className="text-xs text-muted-foreground">
          {isProfit ? 'Take profits when target is reached' : 'Exit position to limit losses'}
        </p>
      </div>

      <div className={`p-4 rounded-lg border ${
        isProfit
          ? 'bg-green-50/50 dark:bg-green-950/20 border-green-200 dark:border-green-900'
          : 'bg-red-50/50 dark:bg-red-950/20 border-red-200 dark:border-red-900'
      }`}>
        <div className="grid grid-cols-2 gap-3">
          {/* Value & Type */}
          <div className="col-span-2 space-y-2">
            <label className="block text-xs font-medium text-muted-foreground">Target Value</label>
            <div className="flex gap-2">
              <div className="flex-1">
                <NumberInput
                  value={Math.abs(value.value)}
                  onChange={(newValue) => onChange({
                    ...value,
                    value: isProfit ? Math.abs(newValue || defaultValue) : -Math.abs(newValue || defaultValue)
                  })}
                  min={0.1}
                  step={value.type === 'percent' ? 0.5 : 1}
                  defaultValue={Math.abs(defaultValue)}
                  required
                />
              </div>
              <select
                value={value.type}
                onChange={(e) => onChange({ ...value, type: e.target.value as ExitValueType })}
                className="w-16 h-10 px-2 rounded-lg border border-input bg-card text-foreground focus:outline-none focus:ring-2 focus:ring-ring text-center"
              >
                {VALUE_TYPES.map((vt) => (
                  <option key={vt.value} value={vt.value}>{vt.label}</option>
                ))}
              </select>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

function TimedExitForm({
  value,
  onChange,
}: {
  value: TimedExit;
  onChange: (timedExit: TimedExit) => void;
}) {
  return (
    <div className="space-y-3">
      <div>
        <label className="block text-sm font-medium text-foreground">Timed Exit</label>
        <p className="text-xs text-muted-foreground">Exit after a specific duration or before market close</p>
      </div>

      <div className="p-4 rounded-lg bg-muted/30 border border-border space-y-4">
        {/* Avoid Overnight */}
        <div className={`flex items-center justify-between gap-3 p-3 rounded-lg border ${
          value.avoidOvernight
            ? 'bg-accent border-border'
            : 'bg-muted/30 border-border'
        }`}>
          <div>
            <span className="text-sm font-medium text-foreground">Avoid Overnight</span>
            <p className="text-xs text-muted-foreground">Exit before market close</p>
          </div>
          <Switch
            checked={value.avoidOvernight}
            onCheckedChange={(checked) => onChange({ ...value, avoidOvernight: checked })}
          />
        </div>

        {/* Timeframe */}
        <div className="space-y-2">
          <label className="block text-xs font-medium text-muted-foreground">
            Maximum Hold Duration
          </label>
          <div className="grid grid-cols-2 gap-3">
            <NumberInput
              value={value.timeframe.multiplier}
              onChange={(newValue) => onChange({
                ...value,
                timeframe: { ...value.timeframe, multiplier: newValue || 30 },
              })}
              min={1}
              max={999}
              step={1}
              defaultValue={30}
              required
            />
            <select
              value={value.timeframe.timespan}
              onChange={(e) => onChange({
                ...value,
                timeframe: { ...value.timeframe, timespan: e.target.value as Timespan },
              })}
              className="w-full h-10 px-3 rounded-lg border border-input bg-card text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
            >
              {TIMESPAN_OPTIONS.map((opt) => (
                <option key={opt.value} value={opt.value}>{opt.label}</option>
              ))}
            </select>
          </div>
        </div>
      </div>
    </div>
  );
}

export function ExitSettingsForm({ value, onChange }: ExitSettingsFormProps) {
  return (
    <div className="space-y-6">
      {/* Stop Loss */}
      <ExitForm
        value={value.stopLoss}
        onChange={(stopLoss) => onChange({ ...value, stopLoss })}
        label="Stop Loss"
        defaultValue={-5}
        isProfit={false}
      />

      {/* Take Profit */}
      <ExitForm
        value={value.takeProfit}
        onChange={(takeProfit) => onChange({ ...value, takeProfit })}
        label="Take Profit"
        defaultValue={10}
        isProfit={true}
      />

      {/* Timed Exit */}
      <TimedExitForm
        value={value.timedExit}
        onChange={(timedExit) => onChange({ ...value, timedExit })}
      />
    </div>
  );
}
