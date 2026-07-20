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
      <CalendarDays className="w-4 h-4 text-muted-foreground" />
      <div className="flex gap-1">
        {days.map(day => (
          <button
            key={day}
            onClick={() => toggleDay(day)}
            className={`px-2 py-1 text-sm rounded-md border transition-colors ${
              selectedDays.includes(day)
                ? 'bg-accent text-foreground font-medium border-border'
                : 'bg-card text-muted-foreground border-border hover:bg-accent hover:text-foreground'
            }`}
          >
            {day.slice(0, 3)}
          </button>
        ))}
      </div>
    </div>
  );
}