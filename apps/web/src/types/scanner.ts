import type { EntrySettings } from './strategy';

// ============================================================================
// Scanner (matches backend ScannerResponse / ScannerCreateRequest)
// ============================================================================

export interface Scanner {
  id?: string;
  userId?: string;
  name: string;
  entrySettings: EntrySettings;
}

// ============================================================================
// Scan run (matches backend ScanRequest / ScanResponse)
// ============================================================================

export interface ScanResultItem {
  ticker: string;
  price: number;
  volume: number;
  float?: number;
}

export interface ScanResponse {
  items: ScanResultItem[];
  timeElapsed: number;
}
