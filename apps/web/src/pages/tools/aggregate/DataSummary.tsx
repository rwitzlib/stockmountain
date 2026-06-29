
import type { StockMarketData } from '../../../types/tools';

interface DataSummaryProps {
  data: StockMarketData;
}

export function DataSummary({ data }: DataSummaryProps) {
  if (!data || !data.results || !Array.isArray(data.results) || data.results.length === 0) {
    return (
      <div className="bg-white rounded-lg shadow-sm p-6">
        <h2 className="text-lg font-semibold mb-4">Summary</h2>
        <p className="text-gray-500">No data available</p>
      </div>
    );
  }

  const stats = {
    totalVolume: data.results.reduce((sum, bar) => sum + bar.v, 0),
    avgPrice: data.results.reduce((sum, bar) => sum + bar.vw, 0) / data.results.length,
    highestPrice: Math.max(...data.results.map(bar => bar.h)),
    lowestPrice: Math.min(...data.results.map(bar => bar.l)),
  };

  return (
    <div className="bg-white rounded-lg shadow-sm p-6">
      <h2 className="text-lg font-semibold mb-4">Summary</h2>
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <div>
          <p className="text-sm text-gray-600">Ticker</p>
          <p className="text-xl font-semibold">{data.ticker}</p>
        </div>
        <div>
          <p className="text-sm text-gray-600">Total Volume</p>
          <p className="text-xl font-semibold">{stats.totalVolume.toLocaleString()}</p>
        </div>
        <div>
          <p className="text-sm text-gray-600">Average Price</p>
          <p className="text-xl font-semibold">${stats.avgPrice.toFixed(2)}</p>
        </div>
        <div>
          <p className="text-sm text-gray-600">Price Range</p>
          <p className="text-xl font-semibold">
            ${stats.lowestPrice.toFixed(2)} - ${stats.highestPrice.toFixed(2)}
          </p>
        </div>
      </div>
    </div>
  );
}
