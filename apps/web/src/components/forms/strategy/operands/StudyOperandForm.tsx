import { Operand } from '../../../../types/strategy';
import { NumberInput } from '../../../ui/NumberInput';

interface StudyOperandFormProps {
  value: Operand;
  onChange: (value: Operand) => void;
}

export function StudyOperandForm({ value, onChange }: StudyOperandFormProps) {
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-12 gap-4">
        <div className="col-span-3">
          <label className="block text-sm font-medium">Study</label>
          <select
            value={value.name || 'rsi'}
            onChange={e => onChange({ ...value, type: 'Study', name: e.target.value })}
            className="mt-1 block w-full rounded-md border border-input bg-background text-foreground shadow-sm focus:border-ring focus:ring-ring"
          >
            <option value="sma">SMA</option>
            <option value="ema">EMA</option>
            <option value="rsi">RSI</option>
            <option value="macd">MACD</option>
            <option value="rvol">RVOL</option>
            <option value="vwap">VWAP</option>
          </select>
        </div>

        <div className="col-span-5">
          <label className="block text-sm font-medium">Parameters</label>
          <input
            type="text"
            value={value.parameters || ''}
            onChange={e => onChange({ ...value, type: 'Study', parameters: e.target.value })}
            placeholder="e.g., 30, 70, 14"
            className="mt-1 block w-full rounded-md border border-input bg-background text-foreground shadow-sm focus:border-ring focus:ring-ring"
          />
        </div>

        <div className="col-span-4">
          <label className="block text-sm font-medium">Modifier</label>
          <select
            value={value.modifier || 'Value'}
            onChange={e => onChange({ ...value, type: 'Study', modifier: e.target.value as "Value" | "Slope" })}
            className="mt-1 block w-full rounded-md border border-input bg-background text-foreground shadow-sm focus:border-ring focus:ring-ring"
          >
            <option value="Value">Value</option>
            <option value="Slope">Slope</option>
          </select>
        </div>
      </div>

      <div>
        <label className="block text-sm font-medium text-gray-700">Timespan</label>
        <div className="grid grid-cols-2 gap-2">
          <NumberInput
            value={value.timeframe?.multiplier || 1}
            onChange={(newValue) => onChange({
              ...value,
              type: 'Study',
              timeframe: {
                multiplier: newValue || 1,
                timespan: value.timeframe?.timespan || 'minute'
              }
            })}
            min={1}
            step={1}
            defaultValue={1}
          />
          <select
            value={value.timeframe?.timespan || 'minute'}
            onChange={e => onChange({
              ...value,
              type: 'Study',
              timeframe: {
                multiplier: value.timeframe?.multiplier || 1,
                timespan: e.target.value
              }
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
