import { StopConfig, TimeFrame, Filter, ScanArgument } from '../../../types/strategy';
import { NumberInput } from '../../ui/NumberInput';

interface ExitFormProps {
  value: {
    StopLoss?: StopConfig;
    ProfitTarget?: StopConfig;
    Timeframe?: TimeFrame;
    Other?: ScanArgument;
  };
  onChange: (value: ExitFormProps['value']) => void;
}

export function ExitForm({ value, onChange }: ExitFormProps) {
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div className="space-y-2">
          <div className="flex items-center space-x-2">
            <input
              type="checkbox"
              id="useStopLoss"
              checked={!!value.StopLoss}
              onChange={e => onChange({...value, StopLoss: e.target.checked ? {
                type: 'percent',
                value: 10
              } : undefined})}
              className="w-4 h-4"
            />
            <label htmlFor="useStopLoss" className="text-sm font-medium">
              Use Stop Loss
            </label>
          </div>
          {value.StopLoss && (
            <div className="space-y-2">
              <select
                id="stopLossType"
                value={value.StopLoss.type}
                onChange={e => onChange({
                  ...value,
                  StopLoss: { ...value.StopLoss, type: e.target.value as 'percent' | 'value' }
                })}
                className="w-full px-3 py-2 border border-input bg-background text-foreground rounded-md focus:outline-none focus:ring-2 focus:ring-ring focus:border-ring"
              >
                <option value="percent">Percentage</option>
                <option value="value">Fixed Value</option>
              </select>
              <NumberInput
                value={value.StopLoss.value}
                onChange={(newValue) => onChange({
                  ...value,
                  StopLoss: { ...value.StopLoss!, value: newValue || 0 }
                })}
                className="w-full px-3 py-2 border border-input bg-background text-foreground rounded-md focus:outline-none focus:ring-2 focus:ring-ring focus:border-ring"
                placeholder={`Enter stop loss ${value.StopLoss.type === 'percent' ? 'percentage' : 'value'}`}
                suffix={value.StopLoss.type === 'percent' ? '%' : '$'}
                step="any"
                defaultValue={0}
              />
            </div>
          )}
        </div>

        <div className="space-y-2">
          <div className="flex items-center space-x-2">
            <input
              type="checkbox"
              id="useProfitTarget"
              checked={!!value.ProfitTarget}
              onChange={e => onChange({...value, ProfitTarget: e.target.checked ? {
                type: 'percent',
                value: 10
              } : undefined})}
              className="w-4 h-4"
            />
            <label htmlFor="useProfitTarget" className="text-sm font-medium">
              Use Profit Target
            </label>
          </div>
          {value.ProfitTarget && (
            <div className="space-y-2">
              <select
                id="profitTargetType"
                value={value.ProfitTarget.type}
                onChange={e => onChange({
                  ...value,
                  ProfitTarget: { ...value.ProfitTarget, type: e.target.value as 'percent' | 'value' }
                })}
                className="w-full px-3 py-2 border border-input bg-background text-foreground rounded-md focus:outline-none focus:ring-2 focus:ring-ring focus:border-ring"
              >
                <option value="percent">Percentage</option>
                <option value="value">Fixed Value</option>
              </select>
              <NumberInput
                value={value.ProfitTarget.value}
                onChange={(newValue) => onChange({
                  ...value,
                  ProfitTarget: { ...value.ProfitTarget!, value: newValue || 0 }
                })}
                className="w-full px-3 py-2 border border-input bg-background text-foreground rounded-md focus:outline-none focus:ring-2 focus:ring-ring focus:border-ring"
                placeholder={`Enter profit target ${value.ProfitTarget.type === 'percent' ? 'percentage' : 'value'}`}
                suffix={value.ProfitTarget.type === 'percent' ? '%' : '$'}
                step="any"
                defaultValue={0}
              />
            </div>
          )}
        </div>
      </div>
    </div>
  );
} 