import { useEffect, useState } from 'react';
import { TrendingUp, TrendingDown, Clock, AlertCircle } from 'lucide-react';

type MarketStatus = 'closed' | 'pre-market' | 'open' | 'after-hours';

interface MarketInfo {
  status: MarketStatus;
  label: string;
  color: string;
  bgColor: string;
  borderColor: string;
  icon: React.ComponentType<any>;
  description: string;
}

export function MarketStatus() {
  const [currentTime, setCurrentTime] = useState(new Date());

  useEffect(() => {
    const timer = setInterval(() => setCurrentTime(new Date()), 1000);
    return () => clearInterval(timer);
  }, []);

  // Get Eastern Time (NYSE timezone)
  const getEasternTime = (date: Date) => {
    return new Date(date.toLocaleString('en-US', { timeZone: 'America/New_York' }));
  };

  const getMarketStatus = (): MarketInfo => {
    const easternTime = getEasternTime(currentTime);
    const dayOfWeek = easternTime.getDay(); // 0 = Sunday, 6 = Saturday
    const hours = easternTime.getHours();
    const minutes = easternTime.getMinutes();
    const currentMinutes = hours * 60 + minutes;

    // Weekend check
    if (dayOfWeek === 0 || dayOfWeek === 6) {
      return {
        status: 'closed',
        label: 'CLOSED',
        color: 'text-red-600 dark:text-red-400',
        bgColor: 'bg-red-100/50 dark:bg-red-950/30',
        borderColor: 'border-red-300 dark:border-red-800',
        icon: AlertCircle,
        description: 'Weekend'
      };
    }

    // Trading hours in minutes since midnight
    const preMarketStart = 4 * 60; // 4:00 AM
    const marketOpen = 9 * 60 + 30; // 9:30 AM
    const marketClose = 16 * 60; // 4:00 PM
    const afterHoursEnd = 20 * 60; // 8:00 PM

    if (currentMinutes >= preMarketStart && currentMinutes < marketOpen) {
      return {
        status: 'pre-market',
        label: 'PRE-MKT',
        color: 'text-yellow-600 dark:text-yellow-400',
        bgColor: 'bg-yellow-100/50 dark:bg-yellow-950/30',
        borderColor: 'border-yellow-300 dark:border-yellow-800',
        icon: Clock,
        description: 'Pre-Market'
      };
    } else if (currentMinutes >= marketOpen && currentMinutes < marketClose) {
      return {
        status: 'open',
        label: 'OPEN',
        color: 'text-green-600 dark:text-green-400',
        bgColor: 'bg-green-100/50 dark:bg-green-950/30',
        borderColor: 'border-green-300 dark:border-green-800',
        icon: TrendingUp,
        description: 'Regular Hours'
      };
    } else if (currentMinutes >= marketClose && currentMinutes < afterHoursEnd) {
      return {
        status: 'after-hours',
        label: 'AFTER',
        color: 'text-blue-600 dark:text-blue-400',
        bgColor: 'bg-blue-100/50 dark:bg-blue-950/30',
        borderColor: 'border-blue-300 dark:border-blue-800',
        icon: TrendingDown,
        description: 'After Hours'
      };
    } else {
      return {
        status: 'closed',
        label: 'CLOSED',
        color: 'text-red-600 dark:text-red-400',
        bgColor: 'bg-red-100/50 dark:bg-red-950/30',
        borderColor: 'border-red-300 dark:border-red-800',
        icon: AlertCircle,
        description: 'Closed'
      };
    }
  };

  const marketInfo = getMarketStatus();
  const Icon = marketInfo.icon;

  return (
    <div className={`flex items-center gap-2 rounded-full ${marketInfo.bgColor} border ${marketInfo.borderColor} px-3 py-1.5 transition-colors duration-300`}>
      <Icon className={`w-3 h-3 ${marketInfo.color}`} />
      <span className={`text-xs font-medium ${marketInfo.color}`}>
        NYSE {marketInfo.label}
      </span>
    </div>
  );
}
