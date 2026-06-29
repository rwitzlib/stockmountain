import { NumberInput } from '../../../ui/NumberInput';

interface PositionConfigFormProps {
  value: {
    startingBalance: number;
    maxConcurrentPositions: number;
    positionSize: number;
  };
  onChange: (value: PositionConfigFormProps['value']) => void;
}

export function PositionConfigForm({ value, onChange }: PositionConfigFormProps) {
  return (
    <div className="space-y-3">
      <h4 className="text-[10px] font-mono uppercase tracking-wider text-gray-500">:: Position Configuration</h4>
      <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
        <div>
          <label className="block text-[10px] font-mono uppercase tracking-wider text-gray-600 mb-2">Init Balance</label>
          <div>
            <NumberInput
              value={value.startingBalance}
              onChange={(newValue) => onChange({
                ...value,
                startingBalance: newValue || 1000
              })}
              min={1000}
              prefix="$"
              defaultValue={1000}
              required
            />
          </div>
        </div>

        <div>
          <label className="block text-[10px] font-mono uppercase tracking-wider text-gray-600 mb-2">Position Size</label>
          <div>
            <NumberInput
              value={value.positionSize}
              onChange={(newValue) => onChange({
                ...value,
                positionSize: newValue || 100
              })}
              min={100}
              prefix="$"
              defaultValue={100}
              required
            />
          </div>
        </div>

        <div>
          <label className="block text-[10px] font-mono uppercase tracking-wider text-gray-600 mb-2">Max Positions</label>
          <div>
            <NumberInput
              value={value.maxConcurrentPositions}
              onChange={(newValue) => onChange({
                ...value,
                maxConcurrentPositions: newValue || 1
              })}
              min={1}
              step={1}
              defaultValue={1}
              required
            />
          </div>
        </div>
      </div>
    </div>
  );
}