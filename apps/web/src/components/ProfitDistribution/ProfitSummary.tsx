
import { formatCurrency } from '../../utils/formatters';
import { ProfitData } from './types';
import { calculateProfitStats } from './utils';

interface ProfitSummaryProps {
  data: ProfitData[];
}

export function ProfitSummary({ data }: ProfitSummaryProps) {
  const stats = calculateProfitStats(data);
  
  return (
    <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
      {Object.entries(stats).map(([strategy, { sum, average, count, winRate }]) => (
        <div key={strategy} className="bg-gray-50 p-4 rounded-lg">
          <h4 className="text-sm font-medium text-gray-600 mb-2 capitalize">{strategy} Strategy</h4>
          <div className="space-y-2">
            <div>
              <p className="text-xs text-gray-500">Total Profit</p>
              <p className={`text-lg font-semibold ${sum >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                {formatCurrency(sum)}
              </p>
            </div>
            <div>
              <p className="text-xs text-gray-500">Average Profit</p>
              <p className={`text-lg font-semibold ${average >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                {formatCurrency(average)}
              </p>
            </div>
            <div>
              <p className="text-xs text-gray-500">Trade Count</p>
              <p className="text-lg font-semibold text-gray-900">{count}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500">Win Rate</p>
              <p className="text-lg font-semibold text-blue-600">
                {winRate.toFixed(1)}%
              </p>
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}
