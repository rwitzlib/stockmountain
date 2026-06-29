import { CalendarIcon } from 'lucide-react';

interface DateRangeFilterProps {
  startDate: string;
  endDate: string;
  onStartDateChange: (date: string) => void;
  onEndDateChange: (date: string) => void;
}

export function DateRangeFilter({
  startDate,
  endDate,
  onStartDateChange,
  onEndDateChange,
}: DateRangeFilterProps) {
  return (
    <div className="flex items-center gap-4">
      <div className="flex items-center gap-2">
        <CalendarIcon className="w-4 h-4 text-gray-500" />
        <input
          type="date"
          value={startDate}
          onChange={(e) => onStartDateChange(e.target.value)}
          className="px-2 py-1 border rounded-md text-sm"
        />
      </div>
      <span className="text-gray-500">to</span>
      <div className="flex items-center gap-2">
        <CalendarIcon className="w-4 h-4 text-gray-500" />
        <input
          type="date"
          value={endDate}
          onChange={(e) => onEndDateChange(e.target.value)}
          className="px-2 py-1 border rounded-md text-sm"
        />
      </div>
    </div>
  );
}