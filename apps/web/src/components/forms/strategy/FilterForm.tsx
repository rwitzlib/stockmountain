import { Filter } from '../../../types/strategy';
import { OperandForm } from './operands/OperandForm';
import { TimeFrameForm } from './TimeFrameForm';

interface FilterFormProps {
  value: Filter;
  onChange: (value: Filter) => void;
}

export function FilterForm({ value, onChange }: FilterFormProps) {
  return (
    <div className="space-y-6">
      <div>
        <label className="block text-sm font-medium">Collection Modifier</label>
        <select
          value={value.collectionModifier}
          onChange={e => onChange({ ...value, collectionModifier: e.target.value as 'ANY' | 'ALL' })}
          className="mt-1 block w-full rounded-lg border border-input bg-card text-foreground focus:border-ring focus:ring-ring"
        >
          <option value="ANY">ANY</option>
          <option value="ALL">ALL</option>
        </select>
      </div>

      <div className="space-y-6">
        <OperandForm
          value={value.firstOperand}
          onChange={operand => onChange({ ...value, firstOperand: operand })}
          label="First Operand"
        />

        <div>
          <label className="block text-sm font-medium">Operator</label>
          <select
            value={value.operator}
            onChange={e => onChange({ ...value, operator: e.target.value as 'gt' | 'lt' | 'eq' })}
            className="mt-1 block w-full rounded-lg border border-input bg-card text-foreground focus:border-ring focus:ring-ring"
          >
            <option value="gt">Greater Than</option>
            <option value="lt">Less Than</option>
            <option value="eq">Equals</option>
          </select>
        </div>

        <OperandForm
          value={value.secondOperand}
          onChange={operand => onChange({ ...value, secondOperand: operand })}
          label="Second Operand"
        />
      </div>

      <TimeFrameForm
        value={value.timeframe}
        onChange={timeframe => onChange({ ...value, timeframe: timeframe })}
      />
    </div>
  );
}