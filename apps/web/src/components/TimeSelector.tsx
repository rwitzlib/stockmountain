import { useState } from 'react';

const timeRanges = [
  { label: '1D', value: '1d' },
  { label: '1W', value: '1w' },
  { label: '1M', value: '1m' },
  { label: '3M', value: '3m' },
  { label: '1Y', value: '1y' },
  { label: 'ALL', value: 'all' },
];

interface TimeSelectorProps {
  onRangeChange: (range: string) => void;
  initialRange?: string;
}

export const TimeSelector = ({ onRangeChange, initialRange = '1d' }: TimeSelectorProps) => {
  const [activeRange, setActiveRange] = useState(initialRange);

  const handleRangeClick = (range: string) => {
    setActiveRange(range);
    onRangeChange(range);
  };

  return (
    <div className="flex gap-1">
      {timeRanges.map(({ label, value }) => (
        <button
          key={value}
          className={`px-3 py-1 text-xs font-mono uppercase transition-all border ${
            activeRange === value
              ? 'bg-primary/20 dark:bg-cyan-950 text-primary dark:text-cyan-400 border-primary dark:border-cyan-700'
              : 'bg-card text-muted-foreground border-border hover:border-primary dark:hover:border-cyan-700 hover:text-primary dark:hover:text-cyan-400'
          }`}
          onClick={() => handleRangeClick(value)}
        >
          {label}
        </button>
      ))}
    </div>
  );
};