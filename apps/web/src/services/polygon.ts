import { StockMarketData, IndicatorsRequest } from '../types/tools';
import { getAuthHeaders } from '../api/authToken';

interface FetchDataParams {
  ticker: string;
  multiplier: number;
  timespan: 'minute' | 'hour' | 'day' | 'week' | 'month' | 'quarter' | 'year';
  from: string;
  to: string;
  indicators?: string[];
}

export async function fetchMarketData({
  ticker,
  multiplier,
  timespan,
  from,
  to,
  indicators = []
}: FetchDataParams): Promise<StockMarketData> {
  try {
    const baseUrl = import.meta.env.VITE_API_URL ?? 'https://stockmountain.io';

    const headers = await getAuthHeaders();

    const requestBody: any = {
      ticker,
      Multiplier: multiplier,
      Timespan: timespan,
      From: from,
      To: to,
      limit: 50000
    };

    if (indicators.length > 0) {
      requestBody.Indicators = indicators;
    }

    const response = await fetch(baseUrl + "/api/stocks", {
      method: 'POST',
      headers,
      body: JSON.stringify(requestBody)
    });
    const data = await response.json();

    // Check for API-specific error responses
    if (data.status === 'ERROR') {
      throw new Error(data.error || 'Polygon API returned an error');
    }

    // Check for HTTP errors
    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }
    
    // Validate response structure
    if (!data.results || !Array.isArray(data.results)) {
      throw new Error('Invalid response format from Polygon API');
    }

    // Handle no data case
    if (data.results.length === 0) {
      throw new Error('No data available for the specified parameters');
    }

    // Validate required fields in the first result
    const firstResult = data.results[0];
    const requiredFields = ['o', 'h', 'l', 'c', 't', 'v', 'vw', 'n'];
    const missingFields = requiredFields.filter(field => !(field in firstResult));
    
    if (missingFields.length > 0) {
      throw new Error(`Missing required fields in response: ${missingFields.join(', ')}`);
    }

    return data;
  } catch (error) {
    // Enhance error message with more context
    const errorMessage = error instanceof Error ? error.message : 'Unknown error';
    throw new Error(`Failed to fetch market data: ${errorMessage}`);
  }
}