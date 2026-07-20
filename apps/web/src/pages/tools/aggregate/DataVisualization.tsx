import { useMemo } from 'react';
import type { StockMarketData } from '../../../types/tools';
import { DataSummary } from './DataSummary';
import { PriceChart } from '../../../components/charts/PriceChart';
import { DataTable } from '../../../components/tables/DataTable';

interface DataVisualizationProps {
  data: StockMarketData;
}

export function DataVisualization({ data }: DataVisualizationProps) {
  const chartData = useMemo(() => data.results, [data]);

  return (
    <div className="space-y-6">
      <DataSummary data={data} />
      
      <div className="rounded-xl border border-border/80 bg-card p-6">
        <h2 className="text-lg font-semibold tracking-tight mb-4">Price Chart</h2>
        <PriceChart data={chartData} />
      </div>

      <div className="rounded-xl border border-border/80 bg-card p-6">
        <h2 className="text-lg font-semibold tracking-tight mb-4">Data Points</h2>
        <DataTable data={chartData} />
      </div>
    </div>
  );
}