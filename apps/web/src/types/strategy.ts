// ============================================================================
// Timeframe & Timespan Types
// ============================================================================

export interface Timeframe {
  multiplier: number;
  timespan: Timespan;
}

export type Timespan = 'minute' | 'hour' | 'day' | 'week' | 'month' | 'quarter' | 'year';

// Legacy alias for backward compatibility
export type TimeFrame = Timeframe;

// ============================================================================
// Position Settings (matches backend StrategyPositionSettings)
// ============================================================================

export type PositionType = 'Fixed' | 'Percentage';

export interface PositionModel {
  type: PositionType;
  size: number;
}

export interface PositionSettings {
  startingBalance: number;
  maxConcurrentPositions: number;
  model: PositionModel;
  allowSimultaneous: boolean;
  cooldown?: Timeframe;
}

// ============================================================================
// Exit Settings (matches backend StrategyExitSettings)
// ============================================================================

export type ExitCandleType = 'PreviousCandle' | 'CurrentCandle';
export type PriceActionType = 'open' | 'high' | 'low' | 'close' | 'vwap' | 'volume';
export type ExitValueType = 'percent' | 'flat';

export interface Exit {
  candleType: ExitCandleType;
  priceActionType: PriceActionType;
  type: ExitValueType;
  value: number;
}

export interface TimedExit {
  avoidOvernight: boolean;
  timeframe?: Timeframe;
}

export interface ExitSettings {
  stopLoss?: Exit;
  takeProfit?: Exit;
  conditionalExit?: string[];
  timedExit?: TimedExit;
}

// ============================================================================
// Entry Settings (matches backend StrategyEntrySettings)
// ============================================================================

export interface EntrySettings {
  filters: string[];
}

// ============================================================================
// Strategy Enums (matching backend)
// ============================================================================

export type StrategyStateType = 'Inactive' | 'Active' | 'Paused' | 'Deleted';
export type VisibilityType = 'Private' | 'Public';
export type TradeType = 'Paper' | 'Live';
export type IntegrationType = 'Default' | 'Schwab' | 'Fidelity' | 'ETrade';

// Legacy aliases for backward compatibility
export type StrategyState = 'active' | 'inactive';
export type StrategyVisibility = 'public' | 'private';

// ============================================================================
// Strategy Interface (matches backend StrategyCreateRequest)
// ============================================================================

export interface Strategy {
  id?: string;
  name: string;
  state: StrategyStateType;
  visibility: VisibilityType;
  type: TradeType;
  integration: IntegrationType;
  positionSettings: PositionSettings;
  exitSettings: ExitSettings;
  entrySettings: EntrySettings;
}

// ============================================================================
// Legacy Types (for backward compatibility with old components)
// ============================================================================

export interface Filter {
  collectionModifier: 'ANY' | 'ALL';
  firstOperand: Operand;
  operator: 'gt' | 'lt' | 'eq';
  secondOperand: Operand;
  timeframe: Timeframe;
}

export interface Operand {
  type: OperandType;
  name?: string;
  parameters?: string;
  value?: number;
  modifier?: 'Value' | 'Slope';
  timeframe?: Timeframe;
}

export interface ScanArgument {
  operator: 'AND' | 'OR';
  filters: Filter[];
}

export type OperandType = 'Study' | 'PriceAction' | 'Fixed';

export interface StopConfig {
  priceActionType: 'open' | 'close' | 'high' | 'low';
  type: 'percent' | 'value';
  value: number;
}

// Legacy position info (for backward compatibility)
export interface LegacyPositionInfo {
  startingBalance: number;
  maxConcurrentPositions: number;
  positionSize: number;
}

// Legacy exit info (for backward compatibility)
export interface LegacyExitInfo {
  stopLoss?: StopConfig;
  profitTarget?: StopConfig;
  timeframe?: Timeframe;
  other?: ScanArgument;
}

// Legacy strategy interface (for components that haven't migrated yet)
export interface LegacyStrategy {
  id: string;
  name: string;
  type: 'Paper' | 'Live';
  integration: 'Default' | 'Schwab';
  state: StrategyState;
  visibility: StrategyVisibility;
  positionInfo?: LegacyPositionInfo;
  exitInfo?: LegacyExitInfo;
  argument?: ScanArgument;
}

// Exit target configuration (legacy alias)
export interface ExitTarget {
  candleType: string;
  type: 'percent' | 'value';
  value: number;
  priceActionType: string;
}

// Timed exit settings (legacy alias)
export interface TimedExitSettings {
  timeframe: Timeframe;
}

// Cooldown settings (legacy alias)
export interface CooldownSettings {
  multiplier: number;
  timespan: string;
}

  // Optimize trades request payload sent to `/strategy/optimize/{strategyId}`
  export interface StrategyOptimizeRequest {
    strategyId: string;
    // Backend expects enum names; we keep these as strings to avoid tight coupling
    type?: string;   // e.g., "Paper" | "Live"
    status?: string; // e.g., "Open" | "Closed"
    filters: string[];
  }

  // Live strategy state (current balance, positions, etc.)
  export interface StrategyStateResponse {
    strategyId: string;
    cashBalance: number;
    /** Total cost basis of all open positions (sum of entry costs) */
    totalEntryCost: number;
    unrealizedPnl: number;
    /** Current market value of all open positions (totalEntryCost + unrealizedPnl) */
    positionValue: number;
    /** Total account value (cashBalance + positionValue) */
    currentBalance: number;
    openPositionsCount: number;
    openTickers: string[];
    cooldowns: Record<string, number>;
    lastTradeAt: number;
    version: number;
  }

  // Balance history entry
  export interface BalanceHistoryEntry {
    date: string;
    cashBalance: number;
    /** Total cost basis of all open positions at this snapshot */
    totalEntryCost: number;
    unrealizedPnl: number;
    /** Current market value of open positions (totalEntryCost + unrealizedPnl) */
    positionValue: number;
    /** Total account value (cashBalance + positionValue) */
    currentBalance: number;
    openPositionsCount: number;
    recordedAt: number;
    snapshotType: string;
  }

  // Balance history response
  export interface BalanceHistoryResponse {
    strategyId: string;
    history: BalanceHistoryEntry[];
  }