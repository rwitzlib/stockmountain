import { useEffect, useState } from 'react';
import { Clock as ClockIcon } from 'lucide-react';

export function Clock() {
  const [time, setTime] = useState(new Date());
  const [showUTC, setShowUTC] = useState(false);

  useEffect(() => {
    const timer = setInterval(() => setTime(new Date()), 1000);
    return () => clearInterval(timer);
  }, []);

  const formatTime = (num: number) => num.toString().padStart(2, '0');

  // Get time components based on timezone setting
  const displayTime = showUTC ? new Date(time.getTime() + time.getTimezoneOffset() * 60000) : time;
  const hours = formatTime(displayTime.getHours());
  const minutes = formatTime(displayTime.getMinutes());
  const seconds = formatTime(displayTime.getSeconds());

  // Get local timezone abbreviation
  const localTimezone = Intl.DateTimeFormat().resolvedOptions().timeZone;
  const timezoneAbbrev = localTimezone.split('/').pop() || 'LOCAL';

  const handleToggle = () => {
    setShowUTC(!showUTC);
  };

  return (
    <div
      className="flex items-center gap-2 rounded-lg bg-card border border-border px-3 py-1.5 cursor-pointer hover:bg-accent transition-colors"
      onClick={handleToggle}
      title={`Click to toggle ${showUTC ? 'local' : 'UTC'} time`}
    >
      <ClockIcon className="w-3 h-3 text-muted-foreground" />
      <span className="font-mono text-xs text-foreground tabular-nums">
        {hours}:{minutes}:{seconds}
      </span>
      <span className="font-mono text-[9px] text-muted-foreground ml-1">
        {showUTC ? 'UTC' : timezoneAbbrev}
      </span>
    </div>
  );
}