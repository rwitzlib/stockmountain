import { CalendarDays } from 'lucide-react';

export type DayOfWeek = 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday' | 'Sunday';

interface DayOfWeekFilterProps {
  selectedDays: DayOfWeek[];
  onChange: (days: DayOfWeek[]) => void;
}

export function DayOfWeekFilter({ selectedDays, onChange }: DayOfWeekFilterProps) {
  const days: DayOfWeek[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

  const toggleDay = (day: DayOfWeek) => {
    const newDays = selectedDays.includes(day)
      ? selectedDays.filter(d => d !== day)
      : [...selectedDays, day];
    onChange(newDays);
  };

  return (
    <div className="flex items-center gap-2">
      <CalendarDays className="w-4 h-4 text-gray-500" />
      <div className="flex gap-1">
        {days.map(day => (
          <button
            key={day}
            onClick={() => toggleDay(day)}
            className={`px-2 py-1 text-sm rounded-md ${
              selectedDays.includes(day)
                ? 'bg-blue-100 text-blue-700 border-blue-300'
                : 'bg-gray-50 text-gray-700 border-gray-200'
            } border hover:bg-blue-50 transition-colors`}
          >
            {day.slice(0, 3)}
          </button>
        ))}
      </div>
    </div>
  );
}