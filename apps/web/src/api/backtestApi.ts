import { getAuthHeaders } from './authToken';
import { BacktestEntry, BacktestRequest } from "../types/backtest";
import { TradingData } from "../types/types";

const BASE_URL = 'https://stockmountain.io/api';


export const backtestApi = {
  getBacktests: async (): Promise<BacktestEntry[]> => {
    const response = await fetch(`${BASE_URL}/backtest`, {
      method: 'GET',
      headers: await getAuthHeaders()
    });

    if (!response.ok) {
      throw new Error('Failed to fetch backtest data');
    }

    const data = await response.json();
    // Ensure we're getting an array
    return Array.isArray(data) ? data : [];
  },

  createBacktest: async (request: BacktestRequest): Promise<BacktestEntry> => {
    const response = await fetch(`${BASE_URL}/backtest`, {
      method: 'POST',
      headers: await getAuthHeaders(),
      body: JSON.stringify(request)
    });

    if (!response.ok) {
      throw new Error('Failed to create backtest');
    }

    return await response.json();
  },

  getBacktestEntry: async (id: string): Promise<BacktestEntry> => {
    const response = await fetch(`${BASE_URL}/backtest/${id}`, {
      method: 'GET',
      headers: await getAuthHeaders()
    });

    if (!response.ok) {
      throw new Error('Failed to fetch backtest entry');
    }

    return await response.json();
  },

  /** Portfolio outcome (stats + equity + taken trades). */
  getBacktestResult: async (id: string): Promise<TradingData> => {
    const response = await fetch(`${BASE_URL}/backtest/result/${id}`, {
      method: 'GET',
      headers: await getAuthHeaders()
    });

    if (!response.ok) {
      throw new Error('Failed to fetch backtest result');
    }

    return await response.json();
  }
};
