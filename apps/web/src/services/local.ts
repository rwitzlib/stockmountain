import { StockMarketData } from '../types/tools';

interface FetchLocalDataParams {
  ticker: string;
  timespan: string;
}

export async function fetchLocalMarketData({
  ticker,
  timespan
}: FetchLocalDataParams): Promise<StockMarketData> {
  const url = `https://localhost:7159/api/tools/aggregate?ticker=${ticker}&timespan=${timespan}`;
  
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error('Failed to fetch market data');
  }
  
  return response.json();
}