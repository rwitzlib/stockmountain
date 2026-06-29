import type { BacktestRequest } from '../types/backtest';

export const defaultBacktestRequest: BacktestRequest = {
  start: '',
  end: '',
  PositionSettings: {
    StartingBalance: 20000,
    AllowSimultaneous: false,
    MaxConcurrentPositions: 1,
    Model: {
      Type: 'Fixed',
      Size: 1000,
    },
    Cooldown: {
      Multiplier: 1,
      Timespan: 'day',
    },
  },
  EntrySettings: {
    Filters: [],
  },
  ExitSettings: {
    StopLoss: {
      CandleType: 'PreviousCandle',
      Type: 'percent',
      Value: -5,
      PriceActionType: 'close',
    },
    TakeProfit: {
      CandleType: 'PreviousCandle',
      Type: 'percent',
      Value: 20,
      PriceActionType: 'close',
    },
    TimedExit: {
      Timeframe: {
        Multiplier: 5,
        Timespan: 'day',
      },
    },
  },
};