
import { TradingEntry } from '../../types/types';
import { DayOfWeek } from './DayOfWeekFilter';
import { ProfitData, ProfitStats } from './types';

export function transformEntriesToProfitData(entries: TradingEntry[]): ProfitData[] {
  return entries.flatMap(entry =>
    entry.results.map(result => {
      const profitData: ProfitData = {
        ticker: result.ticker,
        hold: result.hold.profit,
        high: result.high.profit,
        boughtAt: result.boughtAt
      };
      
      // Add other strategy data if it exists
      if (result.other) {
        profitData.other = result.other.profit;
      }
      
      return profitData;
    })
  );
}

export function filterProfitDataByDateRange(
  data: ProfitData[],
  startDate: string,
  endDate: string,
  startTime: string,
  endTime: string,
  selectedDays: DayOfWeek[]
): ProfitData[] {
  if (!startDate && !endDate && !startTime && !endTime && selectedDays.length === 0) return data;

  return data.filter(item => {
    const boughtAt = new Date(item.boughtAt);
    
    // Check date range
    if (startDate || endDate) {
      const itemDate = boughtAt.toISOString().split('T')[0];
      if (startDate && itemDate < startDate) return false;
      if (endDate && itemDate > endDate) return false;
    }

    // Check time range
    if (startTime || endTime) {
      const hours = boughtAt.getHours();
      const minutes = boughtAt.getMinutes();
      const timeString = `${hours.toString().padStart(2, '0')}:${minutes.toString().padStart(2, '0')}`;
      
      if (startTime && timeString < startTime) return false;
      if (endTime && timeString > endTime) return false;
    }

    // Check day of week
    if (selectedDays.length > 0) {
      const dayOfWeek = boughtAt.toLocaleDateString('en-US', { weekday: 'long' }) as DayOfWeek;
      if (!selectedDays.includes(dayOfWeek)) return false;
    }

    return true;
  });
}

export function calculateProfitStats(data: ProfitData[]): ProfitStats {
  const initialStats = {
    hold: { sum: 0, average: 0, count: 0, winRate: 0 },
    high: { sum: 0, average: 0, count: 0, winRate: 0 }
  } as ProfitStats;
  
  // Check if any data has the "other" strategy
  const hasOtherStrategy = data.some(item => 'other' in item);
  
  if (hasOtherStrategy) {
    initialStats.other = { sum: 0, average: 0, count: 0, winRate: 0 };
  }

  if (data.length === 0) return initialStats;

  // Count winning trades and calculate sums
  const stats = data.reduce((acc, item) => {
    // Process hold and high strategies
    const strategies = ['hold', 'high'] as const;
    
    strategies.forEach(strategy => {
      acc[strategy].sum += item[strategy];
      acc[strategy].count++;
      if (item[strategy] > 0) {
        acc[strategy].winRate++;
      }
    });
    
    // Process other strategy if it exists
    if ('other' in item && item.other !== undefined && acc.other) {
      acc.other.sum += item.other;
      acc.other.count++;
      if (item.other > 0) {
        acc.other.winRate++;
      }
    }
    
    return acc;
  }, initialStats);

  // Calculate averages and convert win count to percentage
  Object.keys(stats).forEach(key => {
    const strategy = key as keyof ProfitStats;
    if (stats[strategy]) {
      const { count } = stats[strategy];
      stats[strategy].average = count > 0 ? stats[strategy].sum / count : 0;
      stats[strategy].winRate = count > 0 ? (stats[strategy].winRate / count) * 100 : 0;
    }
  });

  return stats;
}
