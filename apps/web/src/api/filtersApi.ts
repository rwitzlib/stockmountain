import { authFetch } from './authToken';
import { API_BASE_URL as BASE_URL } from './apiConfig';
import type { FilterFunctionInfo, FilterValidationResult } from '../types/filters';

export const filtersApi = {
  /** Validates expressions against the real backend parser. Batched. */
  validate: async (
    expressions: string[],
    context?: 'scan' | 'backtest' | 'chart',
  ): Promise<FilterValidationResult[]> => {
    const response = await authFetch(`${BASE_URL}/filters/validate`, {
      method: 'POST',
      body: JSON.stringify(context ? { expressions, context } : { expressions }),
    });

    if (!response.ok) {
      throw new Error('Failed to validate filter expressions');
    }

    const data = await response.json();
    return data.results ?? [];
  },

  /** Autocomplete metadata: functions, literals, signatures, snippets. */
  getFunctions: async (context?: 'scan' | 'backtest' | 'chart'): Promise<FilterFunctionInfo[]> => {
    const query = context ? `?context=${context}` : '';
    const response = await authFetch(`${BASE_URL}/filters/functions${query}`, {
      method: 'GET',
    });

    if (!response.ok) {
      throw new Error('Failed to fetch filter functions');
    }

    const data = await response.json();
    return data.functions ?? [];
  },
};
