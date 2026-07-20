import { useState } from 'react';
import { DataInput } from './DataInput';
import { DataVisualization } from './DataVisualization';
import type { StockMarketData } from '../../../types/tools';

export function AggregatePage() {
  const [data, setData] = useState<StockMarketData | null>(null);

  return (
    <div className="p-6">
      <h1 className="text-3xl font-semibold tracking-tight text-foreground mb-6">Data Aggregator</h1>
      
      <div className="space-y-6">
        <DataInput onDataSubmit={setData} />
        {data && <DataVisualization data={data} />}
      </div>
    </div>
  );
}