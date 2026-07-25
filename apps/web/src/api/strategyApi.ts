import { authFetch } from './authToken';
import { Strategy } from '../types/strategy';
import type { StrategyOptimizeRequest, StrategyStateResponse, BalanceHistoryResponse } from '../types/strategy';

import { API_BASE_URL as BASE_URL } from './apiConfig';


export const strategyApi = {
  createStrategy: async (strategy: Strategy): Promise<Strategy> => {
    const response = await authFetch(`${BASE_URL}/Strategy`, {
      method: 'POST',
      body: JSON.stringify(strategy)
    });

    if (!response.ok) {
      throw new Error('Failed to create trading bot');
    }

    return response.json();
  },

  getStrategy: async (id: string): Promise<Strategy> => {
    const response = await authFetch(`${BASE_URL}/Strategy/${id}`, {
    });
    
    if (!response.ok) {
      throw new Error('Failed to fetch trading bot');
    }
    
    return response.json();
  },

  // Get user's own strategies (private)
  getMyStrategies: async (): Promise<Strategy[]> => {
    const response = await authFetch(`${BASE_URL}/Strategy`, {
    });
    
    if (!response.ok) {
      throw new Error('Failed to fetch my trading strategies');
    }
    
    const data = await response.json();
    // Ensure we're getting an array of strategies
    return Array.isArray(data) ? data : data.strategies || data.items || [];
  },

  // Get all public strategies
  getPublicStrategies: async (): Promise<Strategy[]> => {
    const response = await authFetch(`${BASE_URL}/Strategy?visibility=public`, {
    });
    
    if (!response.ok) {
      throw new Error('Failed to fetch public trading strategies');
    }
    
    const data = await response.json();
    // Ensure we're getting an array of strategies
    return Array.isArray(data) ? data : data.strategies || data.items || [];
  },

  // Legacy function - now defaults to public strategies for backward compatibility
  getStrategies: async (): Promise<Strategy[]> => {
    return strategyApi.getPublicStrategies();
  },

  updateStrategy: async (id: string, strategy: Partial<Strategy>): Promise<Strategy> => {
    const response = await authFetch(`${BASE_URL}/Strategy/${id}`, {
      method: 'PUT',
      body: JSON.stringify(strategy)
    });

    if (!response.ok) {
      throw new Error('Failed to update trading bot');
    }

    return response.json();
  },

  // Optimize trades for a strategy using script-based filters
  optimizeStrategy: async (id: string, request: StrategyOptimizeRequest): Promise<any> => {
    const response = await authFetch(`${BASE_URL}/strategy/optimize/${id}`, {
      method: 'POST',
      body: JSON.stringify(request)
    });

    if (!response.ok) {
      throw new Error('Failed to optimize strategy');
    }

    return response.json();
  },

  deleteStrategy: async (id: string): Promise<void> => {
    const response = await authFetch(`${BASE_URL}/Strategy/${id}`, {
      method: 'DELETE',
    });

    if (!response.ok) {
      throw new Error('Failed to delete trading bot');
    }
  },

  // Get current state for a strategy (balance, positions, P/L)
  getStrategyState: async (id: string): Promise<StrategyStateResponse> => {
    const response = await authFetch(`${BASE_URL}/Strategy/${id}/state`, {
    });
    
    if (!response.ok) {
      throw new Error('Failed to fetch strategy state');
    }
    
    return response.json();
  },

  // Get balance history for a strategy
  getBalanceHistory: async (id: string, startDate?: string, endDate?: string): Promise<BalanceHistoryResponse> => {
    const params = new URLSearchParams();
    if (startDate) params.append('startDate', startDate);
    if (endDate) params.append('endDate', endDate);
    
    const queryString = params.toString();
    const url = `${BASE_URL}/Strategy/${id}/balance-history${queryString ? `?${queryString}` : ''}`;
    
    const response = await authFetch(url, {
    });
    
    if (!response.ok) {
      throw new Error('Failed to fetch balance history');
    }
    
    return response.json();
  }
}; 