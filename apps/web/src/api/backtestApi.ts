import { BacktestEntry, BacktestRequest } from "../types/backtest";

const BASE_URL = 'https://api.stockmountain.io/api';

const getAuthHeaders = () => ({
  'Content-Type': 'application/json',
  'Authorization': `Bearer ${localStorage.getItem('accessToken')}`
});

export const backtestApi = {
  getBacktests: async (): Promise<BacktestEntry[]> => {
    const response = await fetch(`${BASE_URL}/backtest`, {
      method: 'GET',
      headers: getAuthHeaders()
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
      headers: getAuthHeaders(),
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
      headers: getAuthHeaders()
    });

    if (!response.ok) {
      throw new Error('Failed to fetch backtest entry');
    }

    return await response.json();
  },

  getBacktestResult: async (id: string): Promise<any> => {
    const response = await fetch(`${BASE_URL}/backtest/result/${id}`, {
      method: 'GET',
      headers: getAuthHeaders()
    });

    if (!response.ok) {
      throw new Error('Failed to fetch backtest result');
    }

    return await response.json();
  }
};
