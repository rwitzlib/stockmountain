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
    <Card className="overflow-hidden cursor-pointer bg-card border border-border hover:border-primary dark:hover:border-cyan-700 hover:shadow-lg hover:shadow-primary/20 dark:hover:shadow-cyan-900/20 transition-all duration-200 hover:-translate-y-[2px]" onClick={handleCardClick}>
      <CardHeader className="space-y-2 border-b border-border pb-3">
        <div className="flex items-center justify-between">
          <CardTitle className="text-base font-mono font-bold uppercase tracking-wider text-foreground">{strategy.name || 'UNNAMED STRATEGY'}</CardTitle>
          {!readOnly && (
            <div onClick={handleSwitchClick}>
              <Switch
                checked={strategyIsActive}
                onCheckedChange={(checked) => onUpdate(strategy.id!, { ...strategy, state: checked ? 'Active' : 'Inactive' })}
              />
            </div>
          )}
          {readOnly && (
            <Badge variant="outline" className="text-[9px] font-mono uppercase bg-muted text-muted-foreground border-border">
              View Only
            </Badge>
          )}
        </div>
        <div className="flex items-center space-x-2 flex-wrap gap-1">
          <Badge className={`text-[9px] font-mono uppercase border ${
            strategy.type === 'Paper' 
              ? 'bg-yellow-100 dark:bg-yellow-950 text-yellow-700 dark:text-yellow-400 border-yellow-300 dark:border-yellow-800' 
              : 'bg-emerald-100 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-400 border-emerald-300 dark:border-emerald-800'
          }`}>
            {strategy.type || 'Paper'}
          </Badge>
          <Badge variant="outline" className="text-[9px] font-mono uppercase bg-primary/10 dark:bg-cyan-950 text-primary dark:text-cyan-400 border-primary/30 dark:border-cyan-800">
            {strategy.integration || 'Default'}
          </Badge>
          {strategyIsActive && (
            <Badge className="text-[9px] font-mono uppercase bg-green-100 dark:bg-green-950 text-green-700 dark:text-green-400 border border-green-300 dark:border-green-700">
              <div className="w-1.5 h-1.5 rounded-full mr-1.5 bg-green-600 dark:bg-green-400 animate-pulse" />
              ACTIVE
            </Badge>
          )}
        </div>
      </CardHeader>
      <CardContent className="space-y-3 pt-3">
        <div className="space-y-2">
          <h4 className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground">::  Position Config</h4>
          <div className="grid grid-cols-2 gap-2 text-[10px] font-mono text-muted-foreground">
            <div><span className="text-primary dark:text-cyan-400">INIT BAL:</span> ${positionSettings.startingBalance.toLocaleString()}</div>
            <div><span className="text-primary dark:text-cyan-400">POS SIZE:</span> {positionSettings.model.type === 'Percentage' ? `${positionSettings.model.size}%` : `$${positionSettings.model.size.toLocaleString()}`}</div>
            <div><span className="text-primary dark:text-cyan-400">MAX POS:</span> {positionSettings.maxConcurrentPositions}</div>
          </div>
        </div>
        
        <div className="space-y-2 border-t border-border pt-2">
          <h4 className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground">::  Exit Strategy</h4>
          <div className="grid grid-cols-1 gap-1 text-[10px] font-mono text-muted-foreground">
            {exitSettings.stopLoss ? (
              <div>
                <span className="text-red-600 dark:text-red-400">STOP LOSS:</span> {exitSettings.stopLoss.value}
                {exitSettings.stopLoss.type === 'percent' ? '%' : '$'}
              </div>
            ) : (
              <div><span className="text-muted-foreground/60">STOP LOSS:</span> None</div>
            )}
            {exitSettings.takeProfit ? (
              <div>
                <span className="text-green-600 dark:text-green-400">TARGET:</span> {exitSettings.takeProfit.value}
                {exitSettings.takeProfit.type === 'percent' ? '%' : '$'}
              </div>
            ) : (
              <div><span className="text-muted-foreground/60">TARGET:</span> None</div>
            )}
            {exitSettings.timedExit?.timeframe ? (
              <div>
                <span className="text-yellow-600 dark:text-yellow-400">TIMEFRAME:</span> {exitSettings.timedExit.timeframe.multiplier} {exitSettings.timedExit.timeframe.timespan}
              </div>
            ) : (
              <div><span className="text-muted-foreground/60">TIMEFRAME:</span> None</div>
            )}
          </div>
        </div>
      </CardContent>
    </Card>
  );
} 