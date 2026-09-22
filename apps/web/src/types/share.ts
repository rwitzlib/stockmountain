/**
 * Mirrors MarketViewer.Contracts.Models.Backtest.BacktestSharePayload (camelCase over the wire).
 * Served by the anonymous GET /api/share/{shareId} endpoint; payloads are immutable snapshots,
 * so this file must keep rendering schemaVersion 1 for the 30-day share lifetime.
 */

import type { BacktestFillSettings } from './backtest';

export const SHARE_SCHEMA_VERSION = 1;

export interface ShareBenchmarkPoint {
  date: string;
  close: number;
}

/**
 * Exactly one branch is populated: full settings when the owner included the strategy
 * configuration, otherwise only counts/flags for the locked teaser panels.
 */
export interface ShareConfig {
  masked: boolean;

  // masked === false
  positionSettings?: {
    startingBalance?: number;
    maxConcurrentPositions?: number;
    allowSimultaneous?: boolean;
    model?: { type?: string; size?: number };
  };
  exitSettings?: {
    stopLoss?: { type?: string; value?: number };
    takeProfit?: { type?: string; value?: number };
    trailingStop?: { type?: string; value?: number; activation?: number };
    timedExit?: {
      timeframe?: { multiplier?: number; timespan?: string };
      avoidOvernight?: boolean;
    };
  };
  entrySettings?: { filters?: string[] };

  /** Always present when the backtest has them: the fill model is context, not strategy IP. */
  fillSettings?: Partial<BacktestFillSettings> | null;

  // masked === true
  entryFilterCount?: number;
  hasStopLoss?: boolean;
  hasTrailingStop?: boolean;
  hasProfitTarget?: boolean;
  hasTimedExit?: boolean;
}

export interface BacktestSharePayload {
  schemaVersion: number;
  createdAt: string;
  expiresAt: string;
  title?: string | null;
  start: string;
  end: string;
  config: ShareConfig;
  /** Same shape as GET /backtest/result/{id}; run through normalizeTradingData before use. */
  result: unknown;
  benchmark?: ShareBenchmarkPoint[] | null;
}

export interface BacktestShareCreateResponse {
  shareId: string;
  expiresAt: string;
}
