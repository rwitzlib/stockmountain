
import { useState } from 'react';
import { TradingEntry } from '../../types/types';
import { DayOfWeek } from './DayOfWeekFilter';
import { Filters } from './Filters';
import { ProfitChart } from './ProfitChart';
import { ProfitSummary } from './ProfitSummary';
import { filterProfitDataByDateRange, transformEntriesToProfitData } from './utils';

interface ProfitDistributionProps {
  entries: TradingEntry[];
}

export function ProfitDistribution({ entries }: ProfitDistributionProps) {
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [startTime, setStartTime] = useState('');
  const [endTime, setEndTime] = useState('');
  const [selectedDays, setSelectedDays] = useState<DayOfWeek[]>([]);

  const allProfitData = transformEntriesToProfitData(entries);
  const filteredData = filterProfitDataByDateRange(
    allProfitData,
    startDate,
    endDate,
    startTime,
    endTime,
    selectedDays
  );

  return (
    <div className="bg-white rounded-lg shadow-md p-6">
      <div className="flex flex-col gap-6">
        <div className="flex flex-col gap-4">
          <h3 className="text-lg font-semibold">Profit Distribution by Trade</h3>
          <Filters
            startDate={startDate}
            endDate={endDate}
            startTime={startTime}
            endTime={endTime}
            selectedDays={selectedDays}
            onStartDateChange={setStartDate}
            onEndDateChange={setEndDate}
            onStartTimeChange={setStartTime}
            onEndTimeChange={setEndTime}
            onDaysChange={setSelectedDays}
          />
        </div>
        
        <ProfitSummary data={filteredData} />
        <ProfitChart data={filteredData} />
      </div>
    </div>
  );
}
