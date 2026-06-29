import { TimeFrame } from '../../../types/strategy';
import { NumberInput } from '../../ui/NumberInput';

interface TimeFrameFormProps {
  value: TimeFrame;
  onChange: (value: TimeFrame) => void;
}

export function TimeFrameForm({ value, onChange }: TimeFrameFormProps) {
  return (
    <div>
      <label className="block text-sm font-medium">Timeframe</label>
      <div className="mt-1 grid grid-cols-2 gap-2">
        <NumberInput
          value={value.multiplier}
          onChange={(newValue) => onChange({
            ...value,
            multiplier: newValue || 1
          })}
          min={1}
          step={1}
          defaultValue={1}
        />
        <select
          value={value.timespan}
          onChange={e => onChange({
            ...value,
            timespan: e.target.value
          })}
          className="block w-full rounded-md border border-input bg-background text-foreground shadow-sm focus:border-ring focus:ring-ring"
        >
          <option value="minute">Minute</option>
          <option value="hour">Hour</option>
          <option value="day">Day</option>
        </select>
      </div>
    </div>
  );
}