
import type { StockMarketData } from '../../../types/tools';

interface DataSummaryProps {
  data: StockMarketData;
}

export function DataSummary({ data }: DataSummaryProps) {
  if (!data || !data.results || !Array.isArray(data.results) || data.results.length === 0) {
    return (
      <div className="rounded-xl border border-border/80 bg-card p-6">
        <h2 className="text-lg font-semibold tracking-tight mb-4">Summary</h2>
        <p className="text-muted-foreground">No data available</p>
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
    <div className="rounded-xl border border-border/80 bg-card p-6">
      <h2 className="text-lg font-semibold tracking-tight mb-4">Summary</h2>
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <div>
          <p className="text-sm text-muted-foreground">Ticker</p>
          <p className="text-xl font-semibold font-mono">{data.ticker}</p>
        </div>
        <div>
          <p className="text-sm text-muted-foreground">Total Volume</p>
          <p className="text-xl font-semibold tabular-nums">{stats.totalVolume.toLocaleString()}</p>
        </div>
        <div>
          <p className="text-sm text-muted-foreground">Average Price</p>
          <p className="text-xl font-semibold tabular-nums">${stats.avgPrice.toFixed(2)}</p>
        </div>
        <div>
          <p className="text-sm text-muted-foreground">Price Range</p>
          <p className="text-xl font-semibold tabular-nums">
            ${stats.lowestPrice.toFixed(2)} - ${stats.highestPrice.toFixed(2)}
          </p>
        </div>
      </div>
    </div>
  );
}
