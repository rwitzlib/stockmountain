import { GripVertical, TrendingUp } from 'lucide-react';
import { StockSearch } from './StockSearch';
import { IndicatorsModal } from '../../modals/IndicatorsModal';
import { useState } from 'react';
import { IndicatorSetup } from '../../../types/tools';

interface ChartHeaderProps {
  symbol: string;
  timeframe: string;
  onSymbolChange: (symbol: string) => void;
  onTimeframeChange: (timeframe: string) => void;
  multiplier: number;
  timespan: 'minute' | 'hour' | 'day' | 'week' | 'year';
  onMultiplierChange: (multiplier: number) => void;
  onTimespanChange: (timespan: 'minute' | 'hour' | 'day' | 'week' | 'year') => void;
  onParamsChange?: (params: { symbol: string, multiplier: number, timespan: 'minute' | 'hour' | 'day' | 'week' | 'year' }) => void;
  indicators?: IndicatorSetup[];
  onIndicatorsChange?: (indicators: IndicatorSetup[]) => void;
}

export function ChartHeader({
  symbol = "SPY",
  onSymbolChange,
  multiplier = 1,
  timespan = 'minute',
  onMultiplierChange,
  onTimespanChange,
  onParamsChange,
  indicators = [],
  onIndicatorsChange
}: ChartHeaderProps) {
  const [isIndicatorsModalOpen, setIsIndicatorsModalOpen] = useState(false);
  
  const handleMultiplierChange = (newValue: number) => {
    onMultiplierChange(newValue);
    onParamsChange?.({ symbol, multiplier: newValue, timespan });
  };

  const handleTimespanChange = (newValue: 'minute' | 'hour' | 'day' | 'week' | 'year') => {
    onTimespanChange(newValue);
    onParamsChange?.({ symbol, multiplier, timespan: newValue });
  };

  const handleSymbolChange = (newSymbol: string) => {
    onSymbolChange(newSymbol);
    onParamsChange?.({ symbol: newSymbol, multiplier, timespan });
  };

  return (
    <div className="p-2 border-b border-border flex items-center justify-between gap-3 bg-card/50 backdrop-blur-sm">
      <div className="flex items-center gap-3">
        <div className="cursor-move" data-drag-handle>
          <GripVertical className="w-4 h-4 text-muted-foreground hover:text-foreground transition-colors" />
        </div>

        <StockSearch
          value={symbol}
          onSubmit={handleSymbolChange}
        />

        <input
          type="text"
          value={multiplier}
          onChange={(e) => {
            const newValue = Math.max(1, parseInt(e.target.value) || 1);
            handleMultiplierChange(newValue);
          }}
          className="w-16 px-2 py-1 rounded-lg border border-input bg-card text-foreground text-xs tabular-nums focus:outline-none focus:border-ring transition-colors"
        />

        <select
          value={timespan}
          onChange={(e) => {
            const value = e.target.value as 'minute' | 'hour' | 'day' | 'week' | 'year';
            handleTimespanChange(value);
          }}
          className="px-2 py-1 rounded-lg border border-input bg-card text-foreground text-xs focus:outline-none focus:border-ring transition-colors"
        >
          <option value="minute">Min</option>
          <option value="hour">Hour</option>
          <option value="day">Day</option>
          <option value="week">Week</option>
          <option value="year">Year</option>
        </select>
      </div>

      <button
        onClick={() => setIsIndicatorsModalOpen(true)}
        className="px-3 py-1 rounded-lg border border-border bg-card text-foreground hover:bg-accent focus:outline-none transition-colors flex items-center gap-2"
        title="Configure Indicators"
      >
        <TrendingUp className="w-4 h-4" />
        <span className="text-xs">Indicators</span>
      </button>

      <IndicatorsModal
        isOpen={isIndicatorsModalOpen}
        onClose={() => setIsIndicatorsModalOpen(false)}
        onApply={onIndicatorsChange || (() => {})}
        currentIndicators={indicators}
      />
    </div>
  );
}
