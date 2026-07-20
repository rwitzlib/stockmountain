import { Filter, ScanArgument, StopConfig, TimeFrame } from '../../../types/strategy';
import { FilterForm } from './FilterForm';
import { StopLossForm } from './exit/StopLossForm';
import { ProfitTargetForm } from './exit/ProfitTargetForm';
import { ExitTimeframeForm } from './exit/ExitTimeframeForm';
import { Switch } from '../../ui/switch';

interface ExitConfigFormProps {
  value: {
    stopLoss?: StopConfig;
    profitTarget?: StopConfig;
    timeframe?: TimeFrame;
    other?: ScanArgument;
  };
  onChange: (value: ExitConfigFormProps['value']) => void;
}

export function ExitConfigForm({ value, onChange }: ExitConfigFormProps) {
  const handleAddFilter = () => {
    onChange({
      ...value,
      other: {
        operator: 'AND',
        filters: [...value.other?.filters || [], {
          collectionModifier: 'ANY',
          firstOperand: {
            type: 'Study',
            name: 'rsi',
            parameters: '14,70,30,ema',
            modifier: 'Value',
            timeframe: {
              multiplier: 1,
              timespan: 'minute'
            }
          },
          operator: 'gt',
          secondOperand: {
            type: 'Fixed',
            value: 0
          },
          timeframe: {
            multiplier: 1,
            timespan: 'minute'
          }
        }]
      }
    })
  };

  const handleRemoveFilter = (index: number) => {
    onChange({
      ...value,
      other: {
        ...value.other,
        filters: value.other?.filters.filter((_, i) => i !== index) || []
      }
    });
  };

  const handleFilterChange = (index: number, filter: Filter) => {
    onChange({
      ...value,
      other: {
        ...value.other,
        filters: value.other?.filters.map((f, i) => i === index ? filter : f) || []
      }
    });
  };

  return (
    <div className="space-y-4">
      <div className="space-y-3">
        <h3 className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">Exit Configuration</h3>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
          <div className="space-y-2">
            <div className="flex items-center gap-3 rounded-lg border border-border p-2 bg-muted/30">
              <Switch
                id="useStopLoss"
                checked={!!value.stopLoss}
                onCheckedChange={(checked) => onChange({
                  ...value,
                  stopLoss: checked ? { type: 'percent', value: -10, priceActionType: 'close' } : undefined
                })}
              />
              <label htmlFor="useStopLoss" className="text-sm font-medium text-foreground">
                Stop Loss
              </label>
            </div>
            {value.stopLoss && (
              <StopLossForm
                value={value.stopLoss}
                onChange={stopLoss => onChange({...value, stopLoss: stopLoss})}
              />
            )}
          </div>

          <div className="space-y-2">
            <div className="flex items-center gap-3 rounded-lg border border-border p-2 bg-muted/30">
              <Switch
                id="useProfitTarget"
                checked={!!value.profitTarget}
                onCheckedChange={(checked) => onChange({
                  ...value,
                  profitTarget: checked ? { type: 'percent', value: 10, priceActionType: 'close' } : undefined
                })}
              />
              <label htmlFor="useProfitTarget" className="text-sm font-medium text-foreground">
                Profit Target
              </label>
            </div>
            {value.profitTarget && (
              <ProfitTargetForm
                value={value.profitTarget}
                onChange={profitTarget => onChange({...value, profitTarget: profitTarget})}
              />
            )}
          </div>
        </div>

        <div className="space-y-2">
          <div className="flex items-center gap-3 rounded-lg border border-border p-2 bg-muted/30">
            <Switch
              id="useTimeframe"
              checked={!!value.timeframe}
              onCheckedChange={(checked) => onChange({
                ...value,
                timeframe: checked ? { multiplier: 1, timespan: 'minute' } : undefined
              })}
            />
            <label htmlFor="useTimeframe" className="text-sm font-medium text-foreground">
              Exit Timeframe
            </label>
          </div>
          {value.timeframe && (
            <ExitTimeframeForm
              value={value.timeframe}
              onChange={timeframe => onChange({...value, timeframe: timeframe})}
            />
          )}
        </div>
      </div>

      <div className="space-y-3">
        <h4 className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">Additional Exit Conditions</h4>
        <div className="space-y-3">
          {value.other?.filters.map((filter, index) => (
            <div key={index} className="relative p-3 rounded-xl border border-border/80 bg-card">
              <button
                type="button"
                aria-label="Remove exit condition"
                onClick={() => handleRemoveFilter(index)}
                className="absolute top-2 right-2 text-muted-foreground hover:text-red-600 dark:hover:text-red-400 font-bold text-lg transition-colors"
              >
                ×
              </button>
              <FilterForm
                value={filter}
                onChange={filter => handleFilterChange(index, filter)}
              />
            </div>
          ))}
          <button
            type="button"
            onClick={handleAddFilter}
            className="w-full py-2 px-4 rounded-lg border border-border bg-card text-foreground hover:bg-accent focus:outline-none transition-colors text-xs font-medium"
          >
            + Add Exit Condition
          </button>
        </div>
      </div>
    </div>
  );
}