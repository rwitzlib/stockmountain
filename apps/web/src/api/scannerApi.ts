import { authFetch } from './authToken';
import { API_BASE_URL as BASE_URL } from './apiConfig';
import type { Scanner, ScanResponse } from '../types/scanner';

export const scannerApi = {
  createScanner: async (scanner: Scanner): Promise<Scanner> => {
    const response = await authFetch(`${BASE_URL}/scanner`, {
      method: 'POST',
      body: JSON.stringify(scanner),
    });

    if (!response.ok) {
      throw new Error('Failed to create scanner');
    }

    return response.json();
  },

  getScanner: async (id: string): Promise<Scanner> => {
    const response = await authFetch(`${BASE_URL}/scanner/${id}`, {});

    if (!response.ok) {
      throw new Error('Failed to fetch scanner');
    }

    return response.json();
  },

  getMyScanners: async (): Promise<Scanner[]> => {
    const response = await authFetch(`${BASE_URL}/scanner`, {});

    if (!response.ok) {
      throw new Error('Failed to fetch scanners');
    }

    const data = await response.json();
    return Array.isArray(data) ? data : data.items || [];
  },

  updateScanner: async (id: string, scanner: Scanner): Promise<Scanner> => {
    const response = await authFetch(`${BASE_URL}/scanner/${id}`, {
      method: 'PUT',
      body: JSON.stringify(scanner),
    });

    if (!response.ok) {
      throw new Error('Failed to update scanner');
    }

    return response.json();
  },

  deleteScanner: async (id: string): Promise<void> => {
    const response = await authFetch(`${BASE_URL}/scanner/${id}`, {
      method: 'DELETE',
    });

    if (!response.ok) {
      throw new Error('Failed to delete scanner');
    }
  },

  /** Runs the filters through the live scan engine (existing stateless POST /scan). */
  runScan: async (filters: string[], completedBarsOnly = false): Promise<ScanResponse> => {
    const response = await authFetch(`${BASE_URL}/scan`, {
      method: 'POST',
      body: JSON.stringify({ filters, completedBarsOnly }),
    });

    if (!response.ok) {
      throw new Error('Scan failed');
    }

    return response.json();
  },
};
