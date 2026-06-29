import { Operand } from '../../../../types/strategy';
import { NumberInput } from '../../../ui/NumberInput';

interface PriceActionOperandFormProps {
  value: Operand; 
  onChange: (value: Operand) => void;
}

export function PriceActionOperandForm({ value, onChange }: PriceActionOperandFormProps) {
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-12 gap-4">
        <div className="col-span-6">
          <label className="block text-sm font-medium">Price Action</label>
          <select
            value={value.name || 'Volume'}
            onChange={e => onChange({ ...value, type: "PriceAction", name: e.target.value })}
            className="mt-1 block w-full rounded-md border border-input bg-background text-foreground shadow-sm focus:border-ring focus:ring-ring"
          >
            <option value="Volume">Volume</option>
            <option value="Vwap">VWAP</option>
          </select>
        </div>

        <div className="col-span-6">
          <label className="block text-sm font-medium">Modifier</label>
          <select
            value={value.modifier || 'Value'}
            onChange={e => onChange({ ...value, type: "PriceAction", modifier: e.target.value as "Value" | "Slope" })}
            className="mt-1 block w-full rounded-md border border-input bg-background text-foreground shadow-sm focus:border-ring focus:ring-ring"
          >
            <option value="Value">Value</option>
            <option value="Slope">Slope</option>
          </select>
        </div>
      </div>

      <div>
        <label className="block text-sm font-medium">Timespan</label>
        <div className="grid grid-cols-2 gap-2">
          <NumberInput
            value={value.timeframe?.multiplier || 1}
            onChange={(newValue) => onChange({
              ...value,
              type: "PriceAction",
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
              type: "PriceAction",
              timeframe: {
                multiplier: value.timeframe?.multiplier || 1,
                timespan: e.target.value
              }
            })}
            className="mt-1 block w-full rounded-md border border-input bg-background text-foreground shadow-sm focus:border-ring focus:ring-ring"
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
