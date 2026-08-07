import { authFetch } from './authToken';
import { API_BASE_URL as BASE_URL } from './apiConfig';
import type { FilterFunctionInfo, FilterValidationResult } from '../types/filters';

export const filtersApi = {
  /** Validates expressions against the real backend parser. Batched. */
  validate: async (expressions: string[]): Promise<FilterValidationResult[]> => {
    const response = await authFetch(`${BASE_URL}/filters/validate`, {
      method: 'POST',
      body: JSON.stringify({ expressions }),
    });

    if (!response.ok) {
      throw new Error('Failed to validate filter expressions');
    }

    const data = await response.json();
    return data.results ?? [];
  },

  /** Autocomplete metadata: functions, literals, signatures, snippets. */
  getFunctions: async (): Promise<FilterFunctionInfo[]> => {
    const response = await authFetch(`${BASE_URL}/filters/functions`, {
      method: 'GET',
    });

    if (!response.ok) {
      throw new Error('Failed to fetch filter functions');
    }

    const data = await response.json();
    return data.functions ?? [];
  },
};
