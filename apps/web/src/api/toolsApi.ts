import { getAuthHeaders } from './authToken';
import type { BarData as LightweightBarData } from "lightweight-charts";
import type { BarData } from "../types/tools";

import { API_BASE_URL as BASE_URL } from './apiConfig';


export type ChartFilterTimespan = 'minute' | 'hour' | 'day' | 'week' | 'year';

export interface ChartFilterScanRequest {
  ticker: string;
  multiplier: number;
  timespan: ChartFilterTimespan;
  from: string;
  to: string;
  filters: string[];
}

export interface ChartFilterScanResponse {
  results: BarData[];
  matchingTimestamps: Array<number | string>;
}

export const toolsApi = {
  filterChartMatches: async (
    request: ChartFilterScanRequest,
  ): Promise<ChartFilterScanResponse> => {
    const payload = {
      Ticker: request.ticker,
      Multiplier: request.multiplier,
      Timespan: request.timespan,
      From: request.from,
      To: request.to,
      Filters: request.filters,
    };

    const response = await fetch(`${BASE_URL}/tools/filter`, {
      method: 'POST',
      headers: await getAuthHeaders(),
      body: JSON.stringify(payload),
    });

    if (!response.ok) {
      throw new Error('Failed to fetch chart filter matches');
    }

    return response.json();
  },
};


