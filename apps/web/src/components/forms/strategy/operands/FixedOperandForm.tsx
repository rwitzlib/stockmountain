import { Operand } from '../../../../types/strategy';
import { NumberInput } from '../../../ui/NumberInput';

interface FixedOperandFormProps {
  value: Operand;
  onChange: (value: Operand) => void;
}

export function FixedOperandForm({ value, onChange }: FixedOperandFormProps) {
  return (
    <div>
      <label className="block text-sm font-medium">Value</label>
      <div className="mt-1">
        <NumberInput
          value={value.value}
          onChange={(newValue) => onChange({ 
            ...value, 
            type: "Fixed", 
            value: newValue || 0 
          })}
          step="any"
          defaultValue={0}
        />
      </div>
    </div>
  );
}