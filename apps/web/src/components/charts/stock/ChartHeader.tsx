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
          <GripVertical className="w-4 h-4 text-muted-foreground hover:text-primary transition-colors" />
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
          className="w-16 px-2 py-1 border border-border bg-background text-foreground dark:text-cyan-400 text-xs font-mono hover:border-primary dark:hover:border-cyan-700 focus:outline-none focus:border-primary dark:focus:border-cyan-500 transition-colors"
        />

        <select
          value={timespan}
          onChange={(e) => {
            const value = e.target.value as 'minute' | 'hour' | 'day' | 'week' | 'year';
            handleTimespanChange(value);
          }}
          className="px-2 py-1 border border-border bg-background text-foreground dark:text-cyan-400 text-xs font-mono uppercase hover:border-primary dark:hover:border-cyan-700 focus:outline-none focus:border-primary dark:focus:border-cyan-500 transition-colors"
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
        className="px-3 py-1 border border-border bg-background dark:bg-gray-900 text-foreground dark:text-cyan-400 hover:bg-muted dark:hover:bg-gray-800 hover:border-primary dark:hover:border-cyan-700 focus:outline-none focus:border-primary dark:focus:border-cyan-500 transition-all flex items-center gap-2"
        title="Configure Indicators"
      >
        <TrendingUp className="w-4 h-4" />
        <span className="text-xs font-mono">Indicators</span>
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
