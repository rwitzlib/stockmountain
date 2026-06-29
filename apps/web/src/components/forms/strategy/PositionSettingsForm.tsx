import { Switch } from '../../ui/switch';
import { NumberInput } from '../../ui/NumberInput';
import type { PositionSettings, PositionType, Timespan } from '../../../types/strategy';

interface PositionSettingsFormProps {
  value: PositionSettings;
  onChange: (value: PositionSettings) => void;
}

const POSITION_TYPES: { value: PositionType; label: string; description: string }[] = [
  { value: 'Fixed', label: 'Fixed $', description: 'Fixed dollar amount per trade' },
  { value: 'Percentage', label: 'Percent %', description: 'Percentage of balance per trade' },
];

const TIMESPAN_OPTIONS: { value: Timespan; label: string }[] = [
  { value: 'minute', label: 'Minutes' },
  { value: 'hour', label: 'Hours' },
  { value: 'day', label: 'Days' },
  { value: 'week', label: 'Weeks' },
];

export function PositionSettingsForm({ value, onChange }: PositionSettingsFormProps) {
  const handleModelTypeChange = (type: PositionType) => {
    onChange({
      ...value,
      model: {
        ...value.model,
        type,
        // Reset size when switching types
        size: type === 'Percentage' ? 10 : 1000,
      },
    });
  };

  const handleCooldownToggle = (enabled: boolean) => {
    onChange({
      ...value,
      cooldown: enabled ? { multiplier: 5, timespan: 'minute' } : undefined,
    });
  };

  return (
    <div className="space-y-6">
      {/* Starting Balance */}
      <div className="space-y-2">
        <label className="block text-sm font-medium text-foreground">
          Starting Balance
        </label>
        <p className="text-xs text-muted-foreground mb-2">
          Initial capital available for trading
        </p>
        <NumberInput
          value={value.startingBalance}
          onChange={(newValue) => onChange({ ...value, startingBalance: newValue || 1000 })}
          min={100}
          prefix="$"
          defaultValue={10000}
          required
        />
      </div>

      {/* Position Sizing Model */}
      <div className="space-y-3">
        <label className="block text-sm font-medium text-foreground">
          Position Size Model
        </label>
        <p className="text-xs text-muted-foreground mb-2">
          How to calculate the size of each position
        </p>
        
        <div className="grid grid-cols-2 gap-2">
          {POSITION_TYPES.map((posType) => (
            <button
              key={posType.value}
              type="button"
              onClick={() => handleModelTypeChange(posType.value)}
              className={`p-3 rounded-lg border-2 text-left transition-all ${
                value.model.type === posType.value
                  ? 'border-primary bg-primary/5 dark:bg-primary/10'
                  : 'border-border hover:border-muted-foreground/50'
              }`}
            >
              <div className={`font-medium text-sm ${
                value.model.type === posType.value ? 'text-primary' : 'text-foreground'
              }`}>
                {posType.label}
              </div>
              <div className="text-xs text-muted-foreground mt-1">
                {posType.description}
              </div>
            </button>
          ))}
        </div>

        <div className="pt-2">
          <label className="block text-xs font-medium text-muted-foreground mb-2">
            {value.model.type === 'Percentage' ? 'Percentage of Balance' : 'Amount per Trade'}
          </label>
          <NumberInput
            value={value.model.size}
            onChange={(newValue) => onChange({
              ...value,
              model: { ...value.model, size: newValue || (value.model.type === 'Percentage' ? 10 : 1000) }
            })}
            min={value.model.type === 'Percentage' ? 1 : 100}
            max={value.model.type === 'Percentage' ? 100 : undefined}
            prefix={value.model.type === 'Percentage' ? '' : '$'}
            suffix={value.model.type === 'Percentage' ? '%' : ''}
            defaultValue={value.model.type === 'Percentage' ? 10 : 1000}
            required
          />
        </div>
      </div>

      {/* Concurrent Positions */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div className="space-y-2">
          <label className="block text-sm font-medium text-foreground">
            Max Concurrent Positions
          </label>
          <p className="text-xs text-muted-foreground mb-2">
            Maximum open positions at once
          </p>
          <NumberInput
            value={value.maxConcurrentPositions}
            onChange={(newValue) => onChange({ ...value, maxConcurrentPositions: newValue || 1 })}
            min={1}
            max={100}
            step={1}
            defaultValue={1}
            required
          />
        </div>

        <div className="space-y-2">
          <label className="block text-sm font-medium text-foreground">
            Allow Simultaneous
          </label>
          <p className="text-xs text-muted-foreground mb-2">
            Multiple positions in same ticker
          </p>
          <div className={`flex items-center justify-between gap-3 rounded-lg border p-3 ${
            value.allowSimultaneous 
              ? 'bg-green-50 dark:bg-green-950/30 border-green-200 dark:border-green-800' 
              : 'bg-muted/30 border-border'
          }`}>
            <span className={`text-sm font-medium ${
              value.allowSimultaneous ? 'text-green-700 dark:text-green-400' : 'text-muted-foreground'
            }`}>
              {value.allowSimultaneous ? 'Enabled' : 'Disabled'}
            </span>
            <Switch
              checked={value.allowSimultaneous}
              onCheckedChange={(checked) => onChange({ ...value, allowSimultaneous: checked })}
            />
          </div>
        </div>
      </div>

      {/* Cooldown Settings */}
      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <div>
            <label className="block text-sm font-medium text-foreground">
              Position Cooldown
            </label>
            <p className="text-xs text-muted-foreground mt-1">
              Wait period after closing a position before entering again
            </p>
          </div>
          <Switch
            checked={!!value.cooldown}
            onCheckedChange={handleCooldownToggle}
          />
        </div>

        {value.cooldown && (
          <div className="grid grid-cols-2 gap-3 p-4 rounded-lg bg-muted/30 border border-border">
            <div className="space-y-2">
              <label className="block text-xs font-medium text-muted-foreground">
                Duration
              </label>
              <NumberInput
                value={value.cooldown.multiplier}
                onChange={(newValue) => onChange({
                  ...value,
                  cooldown: { ...value.cooldown!, multiplier: newValue || 1 }
                })}
                min={1}
                max={999}
                step={1}
                defaultValue={5}
                required
              />
            </div>
            <div className="space-y-2">
              <label className="block text-xs font-medium text-muted-foreground">
                Unit
              </label>
              <select
                value={value.cooldown.timespan}
                onChange={(e) => onChange({
                  ...value,
                  cooldown: { ...value.cooldown!, timespan: e.target.value as Timespan }
                })}
                className="w-full h-10 px-3 rounded-md border border-input bg-background text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
              >
                {TIMESPAN_OPTIONS.map((opt) => (
                  <option key={opt.value} value={opt.value}>
                    {opt.label}
                  </option>
                ))}
              </select>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

