import { TimeFrame } from '../../../../types/strategy';
import { NumberInput } from '../../../ui/NumberInput';

interface ExitTimeframeFormProps {
  value: TimeFrame;
  onChange: (value: TimeFrame) => void;
}

export function ExitTimeframeForm({ value, onChange }: ExitTimeframeFormProps) {
  return (
    <div className="space-y-4">
      <h4 className="text-base font-medium text-gray-900">Exit Timeframe</h4>
      <div className="grid grid-cols-2 gap-4">
        <div>
          <label className="block text-sm font-medium text-gray-700">Multiplier</label>
          <div className="mt-1">
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
          </div>
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700">Timespan</label>
          <select
            value={value.timespan}
            onChange={e => onChange({
              ...value,
              timespan: e.target.value
            })}
            className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-blue-500 focus:ring-blue-500"
          >
            <option value="minute">Minute</option>
            <option value="hour">Hour</option>
            <option value="day">Day</option>
          </select>
        </div>
      </div>
    </div>
  );
}