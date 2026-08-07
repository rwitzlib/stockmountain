import { Switch } from '../../ui/switch';
import { NumberInput } from '../../ui/NumberInput';
import type { PositionSettings, PositionType, Timespan } from '../../../types/strategy';

interface PositionSettingsFormProps {
  value: PositionSettings;
  onChange: (value: PositionSettings) => void;
}

const POSITION_TYPES: { value: PositionType; label: string; hint: string }[] = [
  { value: 'Fixed', label: '$', hint: 'Fixed dollar amount per trade' },
  { value: 'Percentage', label: '%', hint: 'Percentage of balance per trade' },
];

const TIMESPAN_OPTIONS: { value: Timespan; label: string }[] = [
  { value: 'minute', label: 'Minutes' },
  { value: 'hour', label: 'Hours' },
  { value: 'day', label: 'Days' },
  { value: 'week', label: 'Weeks' },
];

function CardLabel({ label, hint }: { label: string; hint: string }) {
  return (
    <div
      className="mb-2 text-[11px] font-medium uppercase tracking-widest text-muted-foreground"
      title={hint}
    >
      {label}
    </div>
  );
}

export function PositionSettingsForm({ value, onChange }: PositionSettingsFormProps) {
  const handleModelTypeChange = (type: PositionType) => {
    if (type === value.model.type) return;
    onChange({
      ...value,
      model: {
        type,
        // Reset size when switching types — a dollar amount makes no sense as a percent
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
    <div className="grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-4">
      {/* Starting balance */}
      <div className="rounded-lg border border-border/60 p-3">
        <CardLabel label="Balance" hint="Initial capital available for trading" />
        <NumberInput
          value={value.startingBalance}
          onChange={(newValue) => onChange({ ...value, startingBalance: newValue || 1000 })}
          min={100}
          prefix="$"
          defaultValue={10000}
          required
        />
      </div>

      {/* Sizing model */}
      <div className="rounded-lg border border-border/60 p-3">
        <CardLabel
          label="Position Size"
          hint="Size of each position: fixed dollars or percent of balance"
        />
        <div className="flex gap-2">
          <div className="flex-1">
            <NumberInput
              value={value.model.size}
              onChange={(newValue) =>
                onChange({
                  ...value,
                  model: {
                    ...value.model,
                    size: newValue || (value.model.type === 'Percentage' ? 10 : 1000),
                  },
                })
              }
              min={value.model.type === 'Percentage' ? 1 : 100}
              max={value.model.type === 'Percentage' ? 100 : undefined}
              defaultValue={value.model.type === 'Percentage' ? 10 : 1000}
              required
            />
          </div>
          <div className="flex h-10 shrink-0 overflow-hidden rounded-lg border border-input">
            {POSITION_TYPES.map((posType) => (
              <button
                key={posType.value}
                type="button"
                title={posType.hint}
                onClick={() => handleModelTypeChange(posType.value)}
                className={`w-9 text-sm transition-colors ${
                  value.model.type === posType.value
                    ? 'bg-accent font-semibold text-foreground'
                    : 'bg-card text-muted-foreground hover:bg-accent/50'
                }`}
              >
                {posType.label}
              </button>
            ))}
          </div>
        </div>
      </div>

      {/* Concurrency */}
      <div className="rounded-lg border border-border/60 p-3">
        <CardLabel label="Max Concurrent" hint="Maximum open positions at once" />
        <NumberInput
          value={value.maxConcurrentPositions}
          onChange={(newValue) => onChange({ ...value, maxConcurrentPositions: newValue || 1 })}
          min={1}
          max={100}
          step={1}
          defaultValue={1}
          required
        />
        <div className="mt-2.5 flex items-center justify-between gap-2">
          <span
            className="text-xs text-muted-foreground"
            title="Allow multiple positions in the same ticker at the same time"
          >
            Allow simultaneous
          </span>
          <Switch
            checked={value.allowSimultaneous}
            onCheckedChange={(checked) => onChange({ ...value, allowSimultaneous: checked })}
          />
        </div>
      </div>

      {/* Cooldown */}
      <div className="rounded-lg border border-border/60 p-3">
        <div className="mb-2 flex items-center justify-between gap-2">
          <span
            className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground"
            title="Wait period after closing a position before entering the same ticker again"
          >
            Cooldown
          </span>
          <Switch checked={!!value.cooldown} onCheckedChange={handleCooldownToggle} />
        </div>
        {value.cooldown ? (
          <div className="flex gap-2">
            <div className="w-20 shrink-0">
              <NumberInput
                value={value.cooldown.multiplier}
                onChange={(newValue) =>
                  onChange({
                    ...value,
                    cooldown: { ...value.cooldown!, multiplier: newValue || 1 },
                  })
                }
                min={1}
                max={999}
                step={1}
                defaultValue={5}
                required
              />
            </div>
            <select
              value={value.cooldown.timespan}
              onChange={(e) =>
                onChange({
                  ...value,
                  cooldown: { ...value.cooldown!, timespan: e.target.value as Timespan },
                })
              }
              className="h-10 flex-1 rounded-lg border border-input bg-card px-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
            >
              {TIMESPAN_OPTIONS.map((opt) => (
                <option key={opt.value} value={opt.value}>{opt.label}</option>
              ))}
            </select>
          </div>
        ) : (
          <p className="text-xs text-muted-foreground/70">Off — re-enter immediately</p>
        )}
      </div>
    </div>
  );
}
