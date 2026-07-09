import { BacktestEntry, BacktestRequestInfo } from '../types/backtest';

/**
 * Normalize backtest request settings from either the current `request`
 * API shape or the legacy `requestDetails` shape.
 */
export function getBacktestRequestInfo(entry: BacktestEntry): BacktestRequestInfo {
  if (entry.request) {
    const req = entry.request;
    const filters = req.entrySettings?.filters || [];
    return {
      positionInfo: {
        startingBalance: req.positionSettings?.startingBalance ?? 10000,
        maxConcurrentPositions: req.positionSettings?.maxConcurrentPositions ?? 1,
        positionSize: req.positionSettings?.model?.size ?? 1000,
        allowSimultaneous: req.positionSettings?.allowSimultaneous ?? false,
        modelType: req.positionSettings?.model?.type ?? 'Fixed',
      },
      exitInfo: {
        stopLoss: req.exitSettings?.stopLoss,
        profitTarget: req.exitSettings?.takeProfit,
        timedExit: req.exitSettings?.timedExit?.timeframe
          ? {
              timeframe: {
                multiplier: req.exitSettings.timedExit.timeframe.multiplier ?? 0,
                timespan: req.exitSettings.timedExit.timeframe.timespan,
              },
            }
          : undefined,
      },
      entryInfo: { filters },
      filters,
    };
  }

  if (entry.requestDetails) {
    return {
      positionInfo: entry.requestDetails.positionInfo || {
        startingBalance: 10000,
        maxConcurrentPositions: 1,
        positionSize: 1000,
      },
      exitInfo: entry.requestDetails.exitInfo,
      entryInfo: entry.requestDetails.entryInfo,
      argument: entry.requestDetails.argument,
      filters: entry.requestDetails.entryInfo?.filters || [],
    };
  }

  return {
    positionInfo: {
      startingBalance: 10000,
      maxConcurrentPositions: 1,
      positionSize: 1000,
    },
    exitInfo: {},
    entryInfo: { filters: [] },
    filters: [],
  };
}
