import { useState } from 'react';
import { Strategy } from '../../types/strategy';
import { ArgumentConfigForm } from './strategy/ArgumentConfigForm';
import { BasicConfigForm } from './strategy/BasicConfigForm';
import { ExitConfigForm } from './strategy/ExitConfigForm';
import { v4 as uuidv4 } from 'uuid';

interface StrategyFormProps {
  onSubmit: (data: Strategy) => void;
  isLoading?: boolean;
  initialData?: Strategy;
}

const defaultFormData: Strategy = {
  id: uuidv4(),
  name: '',
  type: 'Paper',
  integration: 'Default',
  state: 'inactive',
  visibility: 'private',
  positionInfo: {
    startingBalance: 1000,
    maxConcurrentPositions: 1,
    positionSize: 100
  },
  exitInfo: {
  },
  argument: {
    operator: 'AND',
    filters: []
  }
};

export function StrategyForm({ onSubmit, isLoading, initialData }: StrategyFormProps) {
  const [formData, setFormData] = useState<Strategy>(() => {
    if (!initialData) return defaultFormData;

    return {
      ...defaultFormData,
      ...initialData,
      positionInfo: {
        ...defaultFormData.positionInfo,
        ...initialData.positionInfo
      },
      exitInfo: {
        ...defaultFormData.exitInfo,
        ...initialData.exitInfo,
      },
      argument: {
        ...defaultFormData.argument,
        ...initialData.argument,
        filters: initialData.argument?.filters || []
      }
    };
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit(formData);
  };

  return (
    <form onSubmit={handleSubmit} className="flex flex-col h-[calc(100vh-200px)]">
      <div className="flex-1 overflow-y-auto pr-2 space-y-6">
        <BasicConfigForm
          value={{
            id: formData.id,
            name: formData.name,
            type: formData.type,
            integration: formData.integration,
            state: formData.state,
            visibility: formData.visibility,
            positionInfo: formData.positionInfo
          }}
          onChange={value => setFormData(prev => ({
            ...prev,
            name: value.name,
            type: value.type,
            integration: value.integration,
            state: value.state,
            visibility: value.visibility,
            positionInfo: value.positionInfo
          }))}
        />

        <ExitConfigForm
          value={{
            stopLoss: formData.exitInfo?.stopLoss,
            profitTarget: formData.exitInfo?.profitTarget,
            timeframe: formData.exitInfo?.timeframe,
            other: formData.exitInfo?.other,
          }}
          onChange={value => setFormData(prev => ({
            ...prev,
            exitInfo: {
              ...prev.exitInfo,
              stopLoss: value?.stopLoss,
              profitTarget: value?.profitTarget,
              timeframe: value?.timeframe,
              other: value?.other
            }
          }))}
        />

        <ArgumentConfigForm
          value={formData.argument}
          onChange={value => setFormData(prev => ({
            ...prev,
            argument: value
          }))}
        />
      </div>

      <div className="flex items-center justify-between gap-4 pt-6 mt-6 border-t">
        <button
          type="submit"
          disabled={isLoading}
          className="px-6 py-2 bg-primary text-primary-foreground font-medium rounded-lg hover:bg-primary/90 transition-colors disabled:opacity-50"
        >
          {isLoading ? 'Saving Strategy...' : 'Save Strategy'}
        </button>
      </div>
    </form>
  );
}
