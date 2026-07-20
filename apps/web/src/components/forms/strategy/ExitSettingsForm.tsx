import { Switch } from '../../ui/switch';
import { NumberInput } from '../../ui/NumberInput';
import type { 
  ExitSettings, 
  Exit, 
  TimedExit,
  ExitCandleType, 
  PriceActionType, 
  ExitValueType,
  Timespan 
} from '../../../types/strategy';

interface ExitSettingsFormProps {
  value: ExitSettings;
  onChange: (value: ExitSettings) => void;
}

const CANDLE_TYPES: { value: ExitCandleType; label: string }[] = [
  { value: 'CurrentCandle', label: 'Current' },
  { value: 'PreviousCandle', label: 'Previous' },
];

const PRICE_ACTION_TYPES: { value: PriceActionType; label: string }[] = [
  { value: 'close', label: 'Close' },
  { value: 'open', label: 'Open' },
  { value: 'high', label: 'High' },
  { value: 'low', label: 'Low' },
  { value: 'vwap', label: 'VWAP' },
];

const VALUE_TYPES: { value: ExitValueType; label: string }[] = [
  { value: 'percent', label: '%' },
  { value: 'flat', label: '$' },
];

const TIMESPAN_OPTIONS: { value: Timespan; label: string }[] = [
  { value: 'minute', label: 'Minutes' },
  { value: 'hour', label: 'Hours' },
  { value: 'day', label: 'Days' },
];

function ExitForm({ 
  value, 
  onChange, 
  label,
  defaultValue,
  isProfit = false
}: { 
  value: Exit | undefined; 
  onChange: (exit: Exit | undefined) => void;
  label: string;
  defaultValue: number;
  isProfit?: boolean;
}) {
  const handleToggle = (enabled: boolean) => {
    if (enabled) {
      onChange({
        candleType: 'CurrentCandle',
        priceActionType: 'close',
        type: 'percent',
        value: defaultValue,
      });
    } else {
      onChange(undefined);
    }
  };

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <div>
          <label className="block text-sm font-medium text-foreground">{label}</label>
          <p className="text-xs text-muted-foreground">
            {isProfit ? 'Take profits when target is reached' : 'Exit position to limit losses'}
          </p>
        </div>
        <Switch checked={!!value} onCheckedChange={handleToggle} />
      </div>

      {value && (
        <div className={`p-4 rounded-lg border ${
          isProfit 
            ? 'bg-green-50/50 dark:bg-green-950/20 border-green-200 dark:border-green-900' 
            : 'bg-red-50/50 dark:bg-red-950/20 border-red-200 dark:border-red-900'
        }`}>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
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

            {/* Price Action */}
            <div className="space-y-2">
              <label className="block text-xs font-medium text-muted-foreground">Price</label>
              <select
                value={value.priceActionType}
                onChange={(e) => onChange({ ...value, priceActionType: e.target.value as PriceActionType })}
                className="w-full h-10 px-3 rounded-lg border border-input bg-card text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
              >
                {PRICE_ACTION_TYPES.map((pa) => (
                  <option key={pa.value} value={pa.value}>{pa.label}</option>
                ))}
              </select>
            </div>

            {/* Candle Type */}
            <div className="space-y-2">
              <label className="block text-xs font-medium text-muted-foreground">Candle</label>
              <select
                value={value.candleType}
                onChange={(e) => onChange({ ...value, candleType: e.target.value as ExitCandleType })}
                className="w-full h-10 px-3 rounded-lg border border-input bg-card text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
              >
                {CANDLE_TYPES.map((ct) => (
                  <option key={ct.value} value={ct.value}>{ct.label}</option>
                ))}
              </select>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function TimedExitForm({
  value,
  onChange,
}: {
  value: TimedExit | undefined;
  onChange: (timedExit: TimedExit | undefined) => void;
}) {
  const handleToggle = (enabled: boolean) => {
    if (enabled) {
      onChange({
        avoidOvernight: true,
        timeframe: { multiplier: 30, timespan: 'minute' },
      });
    } else {
      onChange(undefined);
    }
  };

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <div>
          <label className="block text-sm font-medium text-foreground">Timed Exit</label>
          <p className="text-xs text-muted-foreground">Exit after a specific duration or before market close</p>
        </div>
        <Switch checked={!!value} onCheckedChange={handleToggle} />
      </div>

      {value && (
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
                value={value.timeframe?.multiplier ?? 30}
                onChange={(newValue) => onChange({
                  ...value,
                  timeframe: {
                    multiplier: newValue || 30,
                    timespan: value.timeframe?.timespan ?? 'minute',
                  },
                })}
                min={1}
                max={999}
                step={1}
                defaultValue={30}
                required
              />
              <select
                value={value.timeframe?.timespan ?? 'minute'}
                onChange={(e) => onChange({
                  ...value,
                  timeframe: {
                    multiplier: value.timeframe?.multiplier ?? 30,
                    timespan: e.target.value as Timespan,
                  },
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
      )}
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

