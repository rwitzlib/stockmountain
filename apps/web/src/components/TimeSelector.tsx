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
          className={`px-3 py-1 rounded-lg text-xs transition-colors border ${
            activeRange === value
              ? 'bg-accent text-foreground font-medium border-border'
              : 'bg-card text-muted-foreground border-border hover:bg-accent hover:text-foreground'
          }`}
          onClick={() => handleRangeClick(value)}
        >
          {label}
        </button>
      ))}
    </div>
  );
};