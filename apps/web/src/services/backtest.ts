
import { BacktestRequest } from '../types/backtest';
import { TradingData } from '../types/types';

export async function fetchBacktestResults(request: BacktestRequest): Promise<TradingData> {
  try {
    const baseUrl = import.meta.env.VITE_API_URL ?? 'https://api.stockmountain.io';
    
    // Get the access token from localStorage
    const accessToken = localStorage.getItem("accessToken");
        
    // Prepare headers with authorization if token exists
    const headers: HeadersInit = {
      'Content-Type': 'application/json',
    };

    if (accessToken) {
      headers['Authorization'] = `Bearer ${accessToken}`;
    }

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
