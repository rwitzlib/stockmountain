import { Strategy, StrategyStateType, ExitValueType, PriceActionType } from '../types/strategy';
import { Switch } from './ui/switch';
import { Card, CardContent, CardHeader, CardTitle } from './ui/card';
import { Badge } from './ui/badge';
import { useNavigate } from 'react-router-dom';

interface StrategyCardProps {
  strategy: Strategy;
  onUpdate: (id: string, data: Partial<Strategy>) => void;
  onDelete: (id: string) => void;
  readOnly?: boolean;
  from?: string; // Origin path for back navigation
}

// Helper to check if strategy is active (handles both old lowercase and new uppercase)
const isActive = (state: string | StrategyStateType): boolean => {
  return state === 'Active' || state === 'active';
};

export function StrategyCard({ strategy, onUpdate, onDelete, readOnly = false, from }: StrategyCardProps) {
  const navigate = useNavigate();
  
  // Get position settings directly from the new structure
  const positionSettings = strategy.positionSettings;

  // Get exit settings directly from the new structure
  const exitSettings = strategy.exitSettings || {};

  const handleSwitchClick = (e: React.MouseEvent) => {
    e.stopPropagation(); // Prevent card click when clicking switch
  };

  const handleCardClick = () => {
    navigate(`/optimus/strategy/${strategy.id}`, {
      state: from ? { from } : undefined
    });
  };

  const strategyIsActive = isActive(strategy.state);

  return (
    <Card className="overflow-hidden cursor-pointer hover:bg-accent/40 hover:border-muted-foreground/40 transition-colors" onClick={handleCardClick}>
      <CardHeader className="space-y-2 border-b border-border pb-3">
        <div className="flex items-center justify-between">
          <CardTitle className="text-base font-semibold tracking-tight text-foreground">{strategy.name || 'Unnamed strategy'}</CardTitle>
          {!readOnly && (
            <div onClick={handleSwitchClick}>
              <Switch
                checked={strategyIsActive}
                onCheckedChange={(checked) => onUpdate(strategy.id!, { ...strategy, state: checked ? 'Active' : 'Inactive' })}
              />
            </div>
          )}
          {readOnly && (
            <Badge variant="outline" className="rounded-full border border-border px-2 py-0.5 text-[10px] font-medium uppercase tracking-wider text-muted-foreground">
              View Only
            </Badge>
          )}
        </div>
        <div className="flex items-center space-x-2 flex-wrap gap-1">
          <Badge className={`rounded-full px-2.5 py-0.5 text-[11px] font-semibold border-transparent ${
            strategy.type === 'Paper' 
              ? 'bg-yellow-500/10 text-yellow-600 dark:text-yellow-400' 
              : 'bg-green-500/10 text-green-600 dark:text-green-400'
          }`}>
            {strategy.type || 'Paper'}
          </Badge>
          <Badge variant="outline" className="rounded-full border border-border px-2 py-0.5 text-[10px] font-medium uppercase tracking-wider text-muted-foreground">
            {strategy.integration || 'Default'}
          </Badge>
          {strategyIsActive && (
            <Badge className="rounded-full bg-green-500/10 px-2.5 py-0.5 text-[11px] font-semibold text-green-600 dark:text-green-400 border-transparent">
              <div className="w-1.5 h-1.5 rounded-full mr-1.5 bg-green-500" />
              Active
            </Badge>
          )}
        </div>
      </CardHeader>
      <CardContent className="space-y-3 pt-3">
        <div className="space-y-2">
          <h4 className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">Position</h4>
          <div className="grid grid-cols-2 gap-2 text-xs text-muted-foreground tabular-nums">
            <div><span className="text-foreground">Balance:</span> ${positionSettings.startingBalance.toLocaleString()}</div>
            <div><span className="text-foreground">Size:</span> {positionSettings.model.type === 'Percentage' ? `${positionSettings.model.size}%` : `$${positionSettings.model.size.toLocaleString()}`}</div>
            <div><span className="text-foreground">Max positions:</span> {positionSettings.maxConcurrentPositions}</div>
          </div>
        </div>
        
        <div className="space-y-2 border-t border-border pt-2">
          <h4 className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">Exit</h4>
          <div className="grid grid-cols-1 gap-1 text-xs text-muted-foreground tabular-nums">
            {exitSettings.stopLoss ? (
              <div>
                <span className="text-red-600 dark:text-red-400">Stop loss:</span> {exitSettings.stopLoss.value}
                {exitSettings.stopLoss.type === 'percent' ? '%' : '$'}
              </div>
            ) : (
              <div><span className="text-muted-foreground/60">Stop loss:</span> None</div>
            )}
            {exitSettings.takeProfit ? (
              <div>
                <span className="text-green-600 dark:text-green-400">Target:</span> {exitSettings.takeProfit.value}
                {exitSettings.takeProfit.type === 'percent' ? '%' : '$'}
              </div>
            ) : (
              <div><span className="text-muted-foreground/60">Target:</span> None</div>
            )}
            {exitSettings.timedExit?.timeframe ? (
              <div>
                <span className="text-yellow-600 dark:text-yellow-400">Timeframe:</span> {exitSettings.timedExit.timeframe.multiplier} {exitSettings.timedExit.timeframe.timespan}
              </div>
            ) : (
              <div><span className="text-muted-foreground/60">Timeframe:</span> None</div>
            )}
          </div>
        </div>
      </CardContent>
    </Card>
  );
} 