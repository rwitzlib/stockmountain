import { Clock } from 'lucide-react';

interface TimeRangeFilterProps {
  startTime: string;
  endTime: string;
  onStartTimeChange: (time: string) => void;
  onEndTimeChange: (time: string) => void;
}

export function TimeRangeFilter({
  startTime,
  endTime,
  onStartTimeChange,
  onEndTimeChange,
}: TimeRangeFilterProps) {
  return (
    <div className="flex items-center gap-4">
      <div className="flex items-center gap-2">
        <Clock className="w-4 h-4 text-muted-foreground" />
        <input
          type="time"
          value={startTime}
          onChange={(e) => onStartTimeChange(e.target.value)}
          className="px-2 py-1 rounded-lg border border-input bg-card text-sm tabular-nums"
        />
      </div>
      <span className="text-muted-foreground">to</span>
      <div className="flex items-center gap-2">
        <Clock className="w-4 h-4 text-muted-foreground" />
        <input
          type="time"
          value={endTime}
          onChange={(e) => onEndTimeChange(e.target.value)}
          className="px-2 py-1 rounded-lg border border-input bg-card text-sm tabular-nums"
        />
      </div>
    </div>
  );
}