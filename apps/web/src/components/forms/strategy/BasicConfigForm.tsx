import { PositionConfigForm } from './basic/PositionConfigForm';
import { Switch } from '../../ui/switch';
import type { StrategyState, StrategyVisibility } from '../../../types/strategy';

interface BasicConfigFormProps {
  value: {
    id: string;
    name: string;
    type: 'Paper' | 'Live';
    integration: 'Default' | 'Schwab';
    state: StrategyState;
    visibility: StrategyVisibility;
    positionInfo: {
      startingBalance: number;
      maxConcurrentPositions: number;
      positionSize: number;
    };
  };
  onChange: (value: BasicConfigFormProps['value']) => void;
}

export function BasicConfigForm({ value, onChange }: BasicConfigFormProps) {
  return (
    <div className="space-y-6">
      <div className="space-y-4">
        <div className="space-y-2">
          <label htmlFor="name" className="text-sm font-medium">
            Strategy name
          </label>
          <input
            type="text"
            id="name"
            value={value.name}
            onChange={e => onChange({...value, name: e.target.value})}
            placeholder="e.g., RSI Pullback Long"
            className="w-full px-3 py-2 border border-input bg-card text-foreground rounded-lg focus:outline-none focus:ring-2 focus:ring-ring focus:border-ring placeholder:text-muted-foreground/70"
          />
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="space-y-2">
            <label className="text-sm font-medium">Status</label>
            <div className={`flex items-center justify-between gap-3 rounded-lg border p-3 ${value.state === 'active' ? 'bg-green-500/10 border-green-200 dark:border-green-900' : 'bg-muted/30 border-border'}`}>
              <div className="text-sm">
                <span className={`font-medium ${value.state === 'active' ? 'text-green-600 dark:text-green-400' : 'text-muted-foreground'}`}>{value.state === 'active' ? 'Active' : 'Inactive'}</span>
                <span className="text-muted-foreground ml-2">Toggle to {value.state === 'active' ? 'deactivate' : 'activate'}</span>
              </div>
              <Switch
                id="state"
                checked={value.state === 'active'}
                onCheckedChange={(checked) => onChange({ ...value, state: checked ? 'active' : 'inactive' })}
              />
            </div>
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">Visibility</label>
            <div className="flex items-center gap-3 rounded-md border p-2">
              <Switch
                id="visibility"
                checked={value.visibility === 'public'}
                onCheckedChange={(checked) => onChange({ ...value, visibility: checked ? 'public' : 'private' })}
              />
              <label htmlFor="visibility" className="text-sm">Public</label>
            </div>
          </div>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="space-y-2">
            <label className="text-sm font-medium">Account mode</label>
            <div className="grid grid-cols-2 gap-1 p-1 rounded-lg border bg-muted/30">
              <button
                type="button"
                onClick={() => onChange({ ...value, type: 'Paper' })}
                className={`px-3 py-2 rounded-md transition-colors ${value.type === 'Paper' ? 'bg-accent text-foreground font-medium' : 'text-muted-foreground hover:bg-accent hover:text-foreground'}`}
              >
                Paper
              </button>
              <button
                type="button"
                onClick={() => onChange({ ...value, type: 'Live' })}
                className={`px-3 py-2 rounded-md transition-colors ${value.type === 'Live' ? 'bg-accent text-foreground font-medium' : 'text-muted-foreground hover:bg-accent hover:text-foreground'}`}
              >
                Live
              </button>
            </div>
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">Integration</label>
            <div className="grid grid-cols-2 gap-1 p-1 rounded-lg border bg-muted/30">
              <button
                type="button"
                onClick={() => onChange({ ...value, integration: 'Default' })}
                className={`px-3 py-2 rounded-md transition-colors ${value.integration === 'Default' ? 'bg-accent text-foreground font-medium' : 'text-muted-foreground hover:bg-accent hover:text-foreground'}`}
              >
                Default
              </button>
              <button
                type="button"
                onClick={() => onChange({ ...value, integration: 'Schwab' })}
                className={`px-3 py-2 rounded-md transition-colors ${value.integration === 'Schwab' ? 'bg-accent text-foreground font-medium' : 'text-muted-foreground hover:bg-accent hover:text-foreground'}`}
              >
                Schwab
              </button>
            </div>
          </div>
        </div>

        
      </div>

      <PositionConfigForm
        value={value.positionInfo}
        onChange={positionInfo => onChange({
          ...value,
          positionInfo: positionInfo
        })}
      />
    </div>
  );
}