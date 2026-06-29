import { useState } from 'react';
import { 
  Strategy, 
  StrategyStateType, 
  VisibilityType, 
  TradeType, 
  IntegrationType 
} from '../../types/strategy';
import { PositionSettingsForm } from './strategy/PositionSettingsForm';
import { ExitSettingsForm } from './strategy/ExitSettingsForm';
import { EntrySettingsForm } from './strategy/EntrySettingsForm';
import { Switch } from '../ui/switch';
import { 
  Settings2, 
  DoorOpen, 
  Filter, 
  Loader2,
  Zap,
  Globe,
  Eye,
  EyeOff
} from 'lucide-react';

interface StrategyFormProps {
  onSubmit: (data: Strategy) => void;
  isLoading?: boolean;
  initialData?: Partial<Strategy>;
}

const TRADE_TYPES: { value: TradeType; label: string; icon: React.ReactNode; description: string }[] = [
  { value: 'Paper', label: 'Paper', icon: <span className="text-base">📝</span>, description: 'Simulated trading' },
  { value: 'Live', label: 'Live', icon: <Zap className="w-4 h-4" />, description: 'Real money trading' },
];

const INTEGRATION_TYPES: { value: IntegrationType; label: string }[] = [
  { value: 'Default', label: 'Default' },
  { value: 'Schwab', label: 'Schwab' },
  { value: 'Fidelity', label: 'Fidelity' },
  { value: 'ETrade', label: 'E*Trade' },
];

const defaultFormData: Strategy = {
  name: '',
  state: 'Inactive',
  visibility: 'Private',
  type: 'Paper',
  integration: 'Default',
  positionSettings: {
    startingBalance: 10000,
    maxConcurrentPositions: 1,
    allowSimultaneous: false,
    model: {
      type: 'Fixed',
      size: 1000,
    },
  },
  exitSettings: {},
  entrySettings: {
    filters: [],
  },
};

type TabId = 'general' | 'position' | 'exit' | 'entry';

interface Tab {
  id: TabId;
  label: string;
  icon: React.ReactNode;
}

const TABS: Tab[] = [
  { id: 'general', label: 'General', icon: <Settings2 className="w-4 h-4" /> },
  { id: 'position', label: 'Position', icon: <span className="text-sm">💰</span> },
  { id: 'exit', label: 'Exit Rules', icon: <DoorOpen className="w-4 h-4" /> },
  { id: 'entry', label: 'Entry Conditions', icon: <Filter className="w-4 h-4" /> },
];

export function StrategyForm({ onSubmit, isLoading, initialData }: StrategyFormProps) {
  const [activeTab, setActiveTab] = useState<TabId>('general');
  const [formData, setFormData] = useState<Strategy>(() => {
    if (!initialData) return defaultFormData;

    return {
      ...defaultFormData,
      ...initialData,
      positionSettings: {
        ...defaultFormData.positionSettings,
        ...initialData.positionSettings,
        model: {
          ...defaultFormData.positionSettings.model,
          ...initialData.positionSettings?.model,
        },
      },
      exitSettings: {
        ...defaultFormData.exitSettings,
        ...initialData.exitSettings,
      },
      entrySettings: {
        ...defaultFormData.entrySettings,
        ...initialData.entrySettings,
        filters: initialData.entrySettings?.filters || [],
      },
    };
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit(formData);
  };

  const isValid = formData.name.trim().length > 0;

  return (
    <form onSubmit={handleSubmit} className="flex flex-col h-[calc(100vh-200px)] max-h-[700px]">
      {/* Tab Navigation */}
      <div className="flex border-b border-border mb-4 overflow-x-auto">
        {TABS.map((tab) => (
          <button
            key={tab.id}
            type="button"
            onClick={() => setActiveTab(tab.id)}
            className={`flex items-center gap-2 px-4 py-3 text-sm font-medium whitespace-nowrap transition-all border-b-2 -mb-px ${
              activeTab === tab.id
                ? 'border-primary text-primary'
                : 'border-transparent text-muted-foreground hover:text-foreground hover:border-border'
            }`}
          >
            {tab.icon}
            {tab.label}
            {tab.id === 'entry' && formData.entrySettings.filters.length > 0 && (
              <span className="ml-1 px-1.5 py-0.5 text-xs rounded-full bg-primary/10 text-primary">
                {formData.entrySettings.filters.length}
              </span>
            )}
          </button>
        ))}
      </div>

      {/* Tab Content */}
      <div className="flex-1 overflow-y-auto pr-2">
        {/* General Tab */}
        {activeTab === 'general' && (
          <div className="space-y-6">
            {/* Strategy Name */}
            <div className="space-y-2">
              <label htmlFor="name" className="block text-sm font-medium text-foreground">
                Strategy Name <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                id="name"
                value={formData.name}
                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                placeholder="e.g., RSI Pullback Strategy"
                className="w-full px-4 py-3 rounded-lg border border-input bg-background text-foreground focus:outline-none focus:ring-2 focus:ring-ring focus:border-ring placeholder:text-muted-foreground/70 text-sm"
                required
              />
            </div>

            {/* Status & Visibility */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {/* Status */}
              <div className="space-y-2">
                <label className="block text-sm font-medium text-foreground">Status</label>
                <div className={`flex items-center justify-between gap-3 rounded-lg border p-4 transition-colors ${
                  formData.state === 'Active' 
                    ? 'bg-green-50 dark:bg-green-950/30 border-green-200 dark:border-green-800' 
                    : 'bg-muted/30 border-border'
                }`}>
                  <div className="flex items-center gap-2">
                    <div className={`w-2 h-2 rounded-full ${
                      formData.state === 'Active' ? 'bg-green-500 animate-pulse' : 'bg-muted-foreground'
                    }`} />
                    <span className={`text-sm font-medium ${
                      formData.state === 'Active' ? 'text-green-700 dark:text-green-400' : 'text-muted-foreground'
                    }`}>
                      {formData.state === 'Active' ? 'Active' : 'Inactive'}
                    </span>
                  </div>
                  <Switch
                    checked={formData.state === 'Active'}
                    onCheckedChange={(checked) => 
                      setFormData({ ...formData, state: checked ? 'Active' : 'Inactive' })
                    }
                  />
                </div>
              </div>

              {/* Visibility */}
              <div className="space-y-2">
                <label className="block text-sm font-medium text-foreground">Visibility</label>
                <div className={`flex items-center justify-between gap-3 rounded-lg border p-4 transition-colors ${
                  formData.visibility === 'Public'
                    ? 'bg-blue-50 dark:bg-blue-950/30 border-blue-200 dark:border-blue-800'
                    : 'bg-muted/30 border-border'
                }`}>
                  <div className="flex items-center gap-2">
                    {formData.visibility === 'Public' ? (
                      <Globe className="w-4 h-4 text-blue-600 dark:text-blue-400" />
                    ) : (
                      <EyeOff className="w-4 h-4 text-muted-foreground" />
                    )}
                    <span className={`text-sm font-medium ${
                      formData.visibility === 'Public' ? 'text-blue-700 dark:text-blue-400' : 'text-muted-foreground'
                    }`}>
                      {formData.visibility}
                    </span>
                  </div>
                  <Switch
                    checked={formData.visibility === 'Public'}
                    onCheckedChange={(checked) =>
                      setFormData({ ...formData, visibility: checked ? 'Public' : 'Private' })
                    }
                  />
                </div>
              </div>
            </div>

            {/* Trade Type */}
            <div className="space-y-3">
              <label className="block text-sm font-medium text-foreground">Account Mode</label>
              <div className="grid grid-cols-2 gap-3">
                {TRADE_TYPES.map((tradeType) => (
                  <button
                    key={tradeType.value}
                    type="button"
                    onClick={() => setFormData({ ...formData, type: tradeType.value })}
                    className={`p-4 rounded-lg border-2 text-left transition-all ${
                      formData.type === tradeType.value
                        ? tradeType.value === 'Live'
                          ? 'border-amber-500 bg-amber-50 dark:bg-amber-950/30'
                          : 'border-primary bg-primary/5 dark:bg-primary/10'
                        : 'border-border hover:border-muted-foreground/50'
                    }`}
                  >
                    <div className="flex items-center gap-2 mb-1">
                      {tradeType.icon}
                      <span className={`font-semibold ${
                        formData.type === tradeType.value
                          ? tradeType.value === 'Live'
                            ? 'text-amber-700 dark:text-amber-400'
                            : 'text-primary'
                          : 'text-foreground'
                      }`}>
                        {tradeType.label}
                      </span>
                    </div>
                    <p className="text-xs text-muted-foreground">{tradeType.description}</p>
                  </button>
                ))}
              </div>
            </div>

            {/* Integration */}
            <div className="space-y-3">
              <label className="block text-sm font-medium text-foreground">Broker Integration</label>
              <div className="grid grid-cols-2 md:grid-cols-4 gap-2">
                {INTEGRATION_TYPES.map((integration) => (
                  <button
                    key={integration.value}
                    type="button"
                    onClick={() => setFormData({ ...formData, integration: integration.value })}
                    className={`px-4 py-3 rounded-lg border transition-all text-sm font-medium ${
                      formData.integration === integration.value
                        ? 'border-primary bg-primary/10 text-primary'
                        : 'border-border bg-muted/30 text-muted-foreground hover:border-muted-foreground/50 hover:text-foreground'
                    }`}
                  >
                    {integration.label}
                  </button>
                ))}
              </div>
              {formData.type === 'Live' && formData.integration === 'Default' && (
                <p className="text-xs text-amber-600 dark:text-amber-400 flex items-center gap-1">
                  <span>⚠️</span>
                  Select a broker for live trading
                </p>
              )}
            </div>
          </div>
        )}

        {/* Position Tab */}
        {activeTab === 'position' && (
          <PositionSettingsForm
            value={formData.positionSettings}
            onChange={(positionSettings) => setFormData({ ...formData, positionSettings })}
          />
        )}

        {/* Exit Tab */}
        {activeTab === 'exit' && (
          <ExitSettingsForm
            value={formData.exitSettings}
            onChange={(exitSettings) => setFormData({ ...formData, exitSettings })}
          />
        )}

        {/* Entry Tab */}
        {activeTab === 'entry' && (
          <EntrySettingsForm
            value={formData.entrySettings}
            onChange={(entrySettings) => setFormData({ ...formData, entrySettings })}
          />
        )}
      </div>

      {/* Footer */}
      <div className="sticky bottom-0 pt-4 mt-4 border-t border-border bg-background">
        <div className="flex items-center justify-between gap-4">
          {/* Summary */}
          <div className="text-xs text-muted-foreground hidden md:block">
            <span className={formData.state === 'Active' ? 'text-green-600 dark:text-green-400' : ''}>
              {formData.state}
            </span>
            {' • '}
            <span>{formData.type}</span>
            {' • '}
            <span>{formData.entrySettings.filters.length} filter{formData.entrySettings.filters.length !== 1 ? 's' : ''}</span>
          </div>

          {/* Actions */}
          <div className="flex items-center gap-3">
            <button
              type="submit"
              disabled={isLoading || !isValid}
              className="px-6 py-2.5 bg-primary text-primary-foreground rounded-lg font-medium hover:bg-primary/90 transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
            >
              {isLoading ? (
                <>
                  <Loader2 className="w-4 h-4 animate-spin" />
                  Saving...
                </>
              ) : (
                'Save Strategy'
              )}
            </button>
          </div>
        </div>
      </div>
    </form>
  );
}
