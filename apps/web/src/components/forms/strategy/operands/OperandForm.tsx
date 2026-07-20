import { Operand, OperandType } from '../../../../types/strategy';
import { StudyOperandForm } from './StudyOperandForm';
import { PriceActionOperandForm } from './PriceActionOperandForm';
import { FixedOperandForm } from './FixedOperandForm';

interface OperandFormProps {
  value: Operand;
  onChange: (value: Operand) => void;
  label?: string;
}

const defaultOperands: Record<OperandType, Operand> = {
  Study: {
    type: 'Study',
    name: 'rsi',
    parameters: '14,70,30,ema',
    modifier: 'Value',
    timeframe: {
      multiplier: 1,
      timespan: 'minute'
    }
  },
  PriceAction: {
    type: 'PriceAction',
    name: 'Volume',
    modifier: 'Value',
    timeframe: {
      multiplier: 1,
      timespan: 'minute'
    }
  },
  Fixed: {
    type: 'Fixed',
    value: 0
  }
};

export function OperandForm({ value, onChange, label }: OperandFormProps) {
  return (
    <div className="operand-form p-4 bg-card text-card-foreground rounded-lg border border-border shadow-sm">
      <div className="space-y-4">
        <div className="select-wrapper">
          <label className="block text-sm font-medium">{label || 'Operand Type'}</label>
          <select
            value={value.type}
            onChange={e => {
              const newType = e.target.value as OperandType;
              onChange(defaultOperands[newType]);
            }}
            className="mt-1 block w-full rounded-lg border border-input bg-card text-foreground focus:border-ring focus:ring-ring"
          >
            <option value="PriceAction">Price Action</option>
            <option value="Study">Study</option>
            <option value="Fixed">Fixed Value</option>
          </select>
        </div>

        <div className="pt-4 border-t border-border/60">
          {value.type === 'Study' && (
            <StudyOperandForm
              value={value}
              onChange={onChange}
            />
          )}

          {value.type === 'PriceAction' && (
            <PriceActionOperandForm
              value={value}
              onChange={onChange}
            />
          )}

          {value.type === 'Fixed' && (
            <FixedOperandForm
              value={value}
              onChange={onChange}
            />
          )}
        </div>
      </div>
    </div>
  );
}