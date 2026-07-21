import { getAuthHeaders } from './authToken';
import { BacktestEntry, BacktestRequest } from "../types/backtest";
import { TradingData } from "../types/types";
import { BacktestSharePayload, BacktestShareCreateResponse } from "../types/share";

import { API_BASE_URL as BASE_URL } from './apiConfig';


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
  },

  /** Mint a new public share link for a completed backtest (authed, owner only). */
  createShare: async (
    id: string,
    options: { includeConfig: boolean; title?: string }
  ): Promise<BacktestShareCreateResponse> => {
    const response = await fetch(`${BASE_URL}/backtest/${id}/share`, {
      method: 'POST',
      headers: await getAuthHeaders(),
      body: JSON.stringify(options)
    });

    if (!response.ok) {
      throw new Error('Failed to create share link');
    }

    return await response.json();
  },

  /**
   * Fetch a public share payload. Deliberately unauthenticated — the share page must
   * work for viewers with no account. Returns null when the share is expired/unknown.
   */
  getShare: async (shareId: string): Promise<BacktestSharePayload | null> => {
    const response = await fetch(`${BASE_URL}/share/${encodeURIComponent(shareId)}`, {
      method: 'GET'
    });

    if (response.status === 404) {
      return null;
    }

    if (!response.ok) {
      throw new Error('Failed to fetch shared backtest');
    }

    return await response.json();
  }
};
