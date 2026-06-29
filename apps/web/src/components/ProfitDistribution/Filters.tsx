import { DateRangeFilter } from './DateRangeFilter';
import { DayOfWeek, DayOfWeekFilter } from './DayOfWeekFilter';
import { TimeRangeFilter } from './TimeRangeFilter';

interface FiltersProps {
  startDate: string;
  endDate: string;
  startTime: string;
  endTime: string;
  selectedDays: DayOfWeek[];
  onStartDateChange: (date: string) => void;
  onEndDateChange: (date: string) => void;
  onStartTimeChange: (time: string) => void;
  onEndTimeChange: (time: string) => void;
  onDaysChange: (days: DayOfWeek[]) => void;
}

export function Filters({
  startDate,
  endDate,
  startTime,
  endTime,
  selectedDays,
  onStartDateChange,
  onEndDateChange,
  onStartTimeChange,
  onEndTimeChange,
  onDaysChange,
}: FiltersProps) {
  return (
    <div className="flex flex-col sm:flex-row justify-end gap-4">
      <DayOfWeekFilter
        selectedDays={selectedDays}
        onChange={onDaysChange}
      />
      <DateRangeFilter
        startDate={startDate}
        endDate={endDate}
        onStartDateChange={onStartDateChange}
        onEndDateChange={onEndDateChange}
      />
      <TimeRangeFilter
        startTime={startTime}
        endTime={endTime}
        onStartTimeChange={onStartTimeChange}
        onEndTimeChange={onEndTimeChange}
      />
    </div>
  );
}