
import { BacktestRequest } from '../types/backtest';
import { TradingData } from '../types/types';
import { getAuthHeaders } from '../api/authToken';
import { API_ORIGIN } from '../api/apiConfig';

export async function fetchBacktestResults(request: BacktestRequest): Promise<TradingData> {
  try {
    const baseUrl = API_ORIGIN;

    const headers = await getAuthHeaders();

    const response = await fetch(baseUrl + "/api/backtest/v3", {
      method: 'POST',
      headers: headers,
      body: JSON.stringify(request)
    });

    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }  

    return await response.json();
  } catch (error) {
    console.error('Error fetching backtest results:', error);
    throw error;
  }
}
