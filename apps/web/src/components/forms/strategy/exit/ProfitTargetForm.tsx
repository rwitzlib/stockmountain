import { StopConfig } from '../../../../types/strategy';
import { NumberInput } from '../../../ui/NumberInput';

interface ProfitTargetFormProps {
  value: StopConfig;
  onChange: (value: StopConfig) => void;
}

export function ProfitTargetForm({ value, onChange }: ProfitTargetFormProps) {
  return (
    <div className="space-y-4">
      <h4 className="text-base font-semibold tracking-tight">Profit Target</h4>
      <div className="grid grid-cols-2 gap-4">
        <div>
          <label className="block text-sm font-medium">Type</label>
          <select
            value={value.type}
            onChange={e => onChange({
              ...value,
              type: e.target.value as 'percent' | 'value'
            })}
            className="mt-1 block w-full rounded-lg border border-input bg-card text-foreground focus:border-ring focus:ring-ring"
          >
            <option value="percent">Percent</option>
            <option value="value">Fixed Value</option>
          </select>
        </div>

        <div>
          <label className="block text-sm font-medium">Value</label>
          <div className="mt-1">
            <NumberInput
              value={value.value}
              onChange={(newValue) => onChange({
                ...value,
                value: newValue || 0
              })}
              step="any"
              suffix={value.type === 'percent' ? '%' : '$'}
              defaultValue={0}
              className="border border-input bg-card text-foreground rounded-lg"
            />
          </div>
        </div>
      </div>
    </div>
  );
}