import { useQuery } from '@tanstack/react-query';
import { strategyApi } from '../../api/strategyApi';
import { Card } from '../ui/card';
import { Badge } from '../ui/badge';
import { formatPrice } from '../../utils/chartUtils';
import { TrendingUp, TrendingDown, Wallet, Activity, Clock, RefreshCw, Briefcase } from 'lucide-react';

interface StrategyStatePanelProps {
  strategyId: string;
  startingBalance: number;
  compact?: boolean;
}

export function StrategyStatePanel({ strategyId, startingBalance, compact = false }: StrategyStatePanelProps) {
  const { data: state, isLoading, error, refetch } = useQuery({
    queryKey: ['strategyState', strategyId],
    queryFn: () => strategyApi.getStrategyState(strategyId),
    refetchInterval: 60000, // Refresh every minute
    enabled: !!strategyId,
  });

  const wrapperClass = compact 
    ? "p-3 h-full" 
    : "p-4 bg-card/50 border border-border";

  if (isLoading) {
    return (
      <div className={wrapperClass}>
        <div className="animate-pulse space-y-2">
          <div className="h-3 bg-muted rounded w-1/3"></div>
          <div className="grid grid-cols-2 gap-2">
            <div className="h-12 bg-muted rounded"></div>
            <div className="h-12 bg-muted rounded"></div>
          </div>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className={wrapperClass}>
        <div className="text-center text-muted-foreground font-mono text-xs">
          <span className="text-yellow-600 dark:text-yellow-400">⚠</span> Unable to load live state
        </div>
      </div>
    );
  }

  // Calculate values from state or use defaults
  const cashBalance = state?.cashBalance ?? startingBalance;
  const totalEntryCost = state?.totalEntryCost ?? 0;
  const unrealizedPnl = state?.unrealizedPnl ?? 0;
  const positionValue = state?.positionValue ?? 0;
  const currentBalance = state?.currentBalance ?? startingBalance;
  const openPositionsCount = state?.openPositionsCount ?? 0;
  const openTickers = state?.openTickers ?? [];
  
  // Calculate total P/L (realized + unrealized)
  const totalPnl = currentBalance - startingBalance;
  const totalPnlPercent = ((totalPnl / startingBalance) * 100);

  // Determine colors based on P/L
  const pnlColorClass = totalPnl >= 0 
    ? 'text-green-600 dark:text-green-400' 
    : 'text-red-600 dark:text-red-400';
  const unrealizedColorClass = unrealizedPnl >= 0 
    ? 'text-green-600 dark:text-green-400' 
    : 'text-red-600 dark:text-red-400';

  if (compact) {
    return (
      <div className={wrapperClass}>
        <div className="flex items-center justify-between mb-2 border-b border-border pb-2">
          <h3 className="text-[10px] font-mono uppercase tracking-wider text-primary dark:text-cyan-400 flex items-center gap-1.5">
            <Activity className="h-3 w-3" />
            Live Status
          </h3>
          <button
            onClick={() => refetch()}
            className="p-0.5 hover:bg-muted rounded transition-colors text-muted-foreground hover:text-primary dark:hover:text-cyan-400"
            title="Refresh"
          >
            <RefreshCw className="h-3 w-3" />
          </button>
        </div>

        {/* Compact Balance Display */}
        <div className="text-center py-2 bg-muted/30 border border-border mb-2">
          <div className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground">Account Balance</div>
          <div className="text-xl font-mono font-bold text-primary dark:text-cyan-400">
            {formatPrice(currentBalance)}
          </div>
          <div className={`text-xs font-mono flex items-center justify-center gap-1 ${pnlColorClass}`}>
            {totalPnl >= 0 ? <TrendingUp className="h-2.5 w-2.5" /> : <TrendingDown className="h-2.5 w-2.5" />}
            {formatPrice(totalPnl)} ({totalPnlPercent >= 0 ? '+' : ''}{totalPnlPercent.toFixed(2)}%)
          </div>
        </div>

        {/* Compact Stats Grid - Cash + Positions = Balance */}
        <div className="grid grid-cols-3 gap-1 mb-2">
          <div className="p-1.5 bg-muted/20 border border-border">
            <div className="flex items-center gap-1 mb-0.5">
              <Wallet className="h-2 w-2 text-muted-foreground" />
              <span className="text-[8px] font-mono uppercase text-muted-foreground">Cash</span>
            </div>
            <div className="text-xs font-mono font-bold text-foreground">
              {formatPrice(cashBalance)}
            </div>
          </div>
          <div className="p-1.5 bg-muted/20 border border-border">
            <div className="flex items-center gap-1 mb-0.5">
              <Briefcase className="h-2 w-2 text-muted-foreground" />
              <span className="text-[8px] font-mono uppercase text-muted-foreground">Positions</span>
            </div>
            <div className="text-xs font-mono font-bold text-foreground">
              {formatPrice(positionValue)}
            </div>
          </div>
          <div className="p-1.5 bg-muted/20 border border-border">
            <div className="flex items-center gap-1 mb-0.5">
              {unrealizedPnl >= 0 
                ? <TrendingUp className="h-2 w-2 text-muted-foreground" />
                : <TrendingDown className="h-2 w-2 text-muted-foreground" />
              }
              <span className="text-[8px] font-mono uppercase text-muted-foreground">Unrealized</span>
            </div>
            <div className={`text-xs font-mono font-bold ${unrealizedColorClass}`}>
              {unrealizedPnl >= 0 ? '+' : ''}{formatPrice(unrealizedPnl)}
            </div>
          </div>
        </div>

        {/* Compact Open Positions */}
        <div className="p-2 bg-muted/20 border border-border">
          <div className="flex items-center justify-between">
            <span className="text-[9px] font-mono uppercase text-muted-foreground">Open Positions</span>
            <Badge 
              variant="outline" 
              className={`text-[10px] font-mono px-1.5 py-0 ${
                openPositionsCount > 0 
                  ? 'bg-primary/10 dark:bg-cyan-950 text-primary dark:text-cyan-400 border-primary/30 dark:border-cyan-700'
                  : 'bg-muted text-muted-foreground border-border'
              }`}
            >
              {openPositionsCount}
            </Badge>
          </div>
          {openTickers.length > 0 ? (
            <div className="flex flex-wrap gap-1 mt-1">
              {openTickers.map(ticker => (
                <Badge 
                  key={ticker}
                  variant="outline"
                  className="text-[9px] font-mono px-1 py-0 bg-yellow-100/50 dark:bg-yellow-950/30 text-yellow-700 dark:text-yellow-400 border-yellow-300 dark:border-yellow-700"
                >
                  {ticker}
                </Badge>
              ))}
            </div>
          ) : (
            <div className="text-[10px] font-mono text-muted-foreground mt-0.5">No open positions</div>
          )}
        </div>
      </div>
    );
  }

  return (
    <Card className="p-4 bg-card/50 border border-border">
      <div className="flex items-center justify-between mb-4 border-b border-border pb-3">
        <h3 className="text-xs font-mono uppercase tracking-wider text-primary dark:text-cyan-400 flex items-center gap-2">
          <Activity className="h-3 w-3" />
          Live Status
        </h3>
        <button
          onClick={() => refetch()}
          className="p-1 hover:bg-muted rounded transition-colors text-muted-foreground hover:text-primary dark:hover:text-cyan-400"
          title="Refresh"
        >
          <RefreshCw className="h-3 w-3" />
        </button>
      </div>

      {/* Main Balance Display */}
      <div className="space-y-4">
        {/* Account Balance - Large Display */}
        <div className="text-center py-3 bg-muted/30 border border-border">
          <div className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground mb-1">
            Account Balance
          </div>
          <div className="text-2xl font-mono font-bold text-primary dark:text-cyan-400">
            {formatPrice(currentBalance)}
          </div>
          <div className={`text-sm font-mono flex items-center justify-center gap-1 mt-1 ${pnlColorClass}`}>
            {totalPnl >= 0 ? <TrendingUp className="h-3 w-3" /> : <TrendingDown className="h-3 w-3" />}
            {formatPrice(totalPnl)} ({totalPnlPercent >= 0 ? '+' : ''}{totalPnlPercent.toFixed(2)}%)
          </div>
        </div>

        {/* Stats Grid - Cash + Positions = Balance */}
        <div className="grid grid-cols-3 gap-2">
          {/* Cash */}
          <div className="p-3 bg-muted/20 border border-border">
            <div className="flex items-center gap-1.5 mb-1">
              <Wallet className="h-3 w-3 text-muted-foreground" />
              <span className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground">Cash</span>
            </div>
            <div className="text-lg font-mono font-bold text-foreground">
              {formatPrice(cashBalance)}
            </div>
          </div>

          {/* Position Value */}
          <div className="p-3 bg-muted/20 border border-border">
            <div className="flex items-center gap-1.5 mb-1">
              <Briefcase className="h-3 w-3 text-muted-foreground" />
              <span className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground">Positions</span>
            </div>
            <div className="text-lg font-mono font-bold text-foreground">
              {formatPrice(positionValue)}
            </div>
            {totalEntryCost > 0 && (
              <div className={`text-[9px] font-mono ${unrealizedColorClass}`}>
                Cost: {formatPrice(totalEntryCost)} ({unrealizedPnl >= 0 ? '+' : ''}{formatPrice(unrealizedPnl)})
              </div>
            )}
          </div>

          {/* Unrealized P/L */}
          <div className="p-3 bg-muted/20 border border-border">
            <div className="flex items-center gap-1.5 mb-1">
              {unrealizedPnl >= 0 
                ? <TrendingUp className="h-3 w-3 text-muted-foreground" />
                : <TrendingDown className="h-3 w-3 text-muted-foreground" />
              }
              <span className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground">Unrealized</span>
            </div>
            <div className={`text-lg font-mono font-bold ${unrealizedColorClass}`}>
              {unrealizedPnl >= 0 ? '+' : ''}{formatPrice(unrealizedPnl)}
            </div>
          </div>
        </div>

        {/* Open Positions */}
        <div className="p-3 bg-muted/20 border border-border">
          <div className="flex items-center justify-between mb-2">
            <span className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground">
              Open Positions
            </span>
            <Badge 
              variant="outline" 
              className={`text-xs font-mono ${
                openPositionsCount > 0 
                  ? 'bg-primary/10 dark:bg-cyan-950 text-primary dark:text-cyan-400 border-primary/30 dark:border-cyan-700'
                  : 'bg-muted text-muted-foreground border-border'
              }`}
            >
              {openPositionsCount}
            </Badge>
          </div>
          
          {openTickers.length > 0 ? (
            <div className="flex flex-wrap gap-1">
              {openTickers.map(ticker => (
                <Badge 
                  key={ticker}
                  variant="outline"
                  className="text-[10px] font-mono bg-yellow-100/50 dark:bg-yellow-950/30 text-yellow-700 dark:text-yellow-400 border-yellow-300 dark:border-yellow-700"
                >
                  {ticker}
                </Badge>
              ))}
            </div>
          ) : (
            <div className="text-xs font-mono text-muted-foreground">
              No open positions
            </div>
          )}
        </div>

        {/* Last Trade Time */}
        {state?.lastTradeAt && state.lastTradeAt > 0 && (
          <div className="flex items-center gap-2 text-[10px] font-mono text-muted-foreground">
            <Clock className="h-3 w-3" />
            <span>Last trade: {new Date(state.lastTradeAt * 1000).toLocaleString()}</span>
          </div>
        )}
      </div>
    </Card>
  );
}

