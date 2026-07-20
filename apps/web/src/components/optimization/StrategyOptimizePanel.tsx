import { useEffect, useMemo, useState, useRef } from 'react';
import { Button } from '../ui/button';
import { Card } from '../ui/card';
import { Input } from '../ui/input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../ui/table';
import { toast } from '../../hooks/use-toast';
import { strategyApi } from '../../api/strategyApi';
import type { StrategyOptimizeRequest } from '../../types/strategy';
import type { FilterHistoryEntry, FilterItem } from '../../types/filters';
import { FilterComposer, type FilterComposerRef } from '../filters/FilterComposer';
import { FilterList } from '../filters/FilterList';

interface StrategyOptimizePanelProps {
  strategyId: string;
  onOptimizationResult?: (trades: any[]) => void;
}

const HISTORY_LIMIT = 20;

const createFilter = (expression: string): FilterItem => ({
  id: crypto.randomUUID(),
  enabled: true,
  expression,
});

  const summarizeResult = (res: any): string | undefined => {
    try {
      if (!res) return undefined;
      const total = res?.totalTrades ?? res?.TotalTrades;
      const pnl = res?.totalProfit ?? res?.TotalProfit;
      const wr = res?.winRate ?? res?.WinRate;
      if (total != null && pnl != null) {
        const wrPct = typeof wr === 'number' ? (wr > 1 ? wr : wr * 100) : undefined;
        return `Trades: ${total}, PnL: ${pnl.toFixed?.(2) ?? pnl}${wrPct != null ? `, WinRate: ${wrPct.toFixed?.(1)}%` : ''}`;
      }
      if (Array.isArray(res?.trades) || Array.isArray(res?.Trades)) {
        const trades = res.trades ?? res.Trades;
        return `Trades: ${trades.length}`;
      }
      return undefined;
    } catch {
      return undefined;
    }
  };

export function StrategyOptimizePanel({ strategyId, onOptimizationResult }: StrategyOptimizePanelProps) {
  const [filters, setFilters] = useState<FilterItem[]>([]);
  const [history, setHistory] = useState<FilterHistoryEntry<StrategyOptimizeRequest>[]>([]);
  const [tradeType, setTradeType] = useState<string | undefined>(undefined);
  const [tradeStatus, setTradeStatus] = useState<string | undefined>(undefined);
  const [isSubmitting, setIsSubmitting] = useState(false);
  
  const filterComposerRef = useRef<FilterComposerRef>(null);

  const storageKey = useMemo(() => `optimize-history:${strategyId}`, [strategyId]);

  useEffect(() => {
    try {
      const raw = localStorage.getItem(storageKey);
      if (raw) {
        const parsed = JSON.parse(raw) as FilterHistoryEntry<StrategyOptimizeRequest>[];
        setHistory(parsed);
      }
    } catch (error) {
      console.warn('Failed to load optimization history', error);
    }
  }, [storageKey]);

  const persistHistory = (entries: FilterHistoryEntry<StrategyOptimizeRequest>[]) => {
    setHistory(entries);
    try {
      localStorage.setItem(storageKey, JSON.stringify(entries));
    } catch (error) {
      console.warn('Unable to persist optimization history', error);
    }
  };

  const handleAddFilter = (expression: string) => {
    if (!expression.trim()) return;
    setFilters(prev => [createFilter(expression.trim()), ...prev]);
  };

  const handleToggleFilter = (id: string) => {
    setFilters(prev => prev.map(filter => (filter.id === id ? { ...filter, enabled: !filter.enabled } : filter)));
  };

  const handleRemoveFilter = (id: string) => {
    setFilters(prev => prev.filter(filter => filter.id !== id));
  };

  const handleClearFilters = () => setFilters([]);

  const handleEditFilter = (id: string) => {
    const filter = filters.find(f => f.id === id);
    if (filter && filterComposerRef.current) {
      filterComposerRef.current.setExpression(filter.expression);
      handleRemoveFilter(id);
    }
  };

  const loadFromHistory = async (entry: FilterHistoryEntry<StrategyOptimizeRequest>) => {
    setFilters(entry.filters.map(expr => createFilter(expr)));
    setTradeType(entry.request.type);
    setTradeStatus(entry.request.status);
    
    // Automatically run the optimization to show results
    try {
      setIsSubmitting(true);
      const result = await strategyApi.optimizeStrategy(strategyId, entry.request);
      
      // Extract trades from result and pass to parent
      const trades = result?.trades ?? result?.Trades ?? [];
      if (onOptimizationResult) {
        onOptimizationResult(trades);
      }
      
      toast({ 
        title: 'Optimization loaded', 
        description: trades.length > 0 ? `Showing ${trades.length} filtered trades` : 'No trades match the filters'
      });
    } catch (error: any) {
      toast({
        title: 'Failed to load optimization',
        description: error?.message || 'Unknown error',
        variant: 'destructive',
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  const deleteHistoryEntry = (id: string) => {
    persistHistory(history.filter(item => item.id !== id));
  };

  const runOptimize = async () => {
    const enabledFilters = filters.filter(filter => filter.enabled).map(filter => filter.expression);
    if (enabledFilters.length === 0) {
      toast({ title: 'No filters', description: 'Please add or enable at least one filter.' });
      return;
    }

    const payload: StrategyOptimizeRequest = {
      strategyId,
      type: tradeType,
      status: tradeStatus,
      filters: enabledFilters,
    };

    try {
      setIsSubmitting(true);
      const result = await strategyApi.optimizeStrategy(strategyId, payload);
      const summary = summarizeResult(result);
      
      // Extract trades from result and pass to parent
      const trades = result?.trades ?? result?.Trades ?? [];
      if (onOptimizationResult) {
        onOptimizationResult(trades);
      }
      
      const entry: FilterHistoryEntry<StrategyOptimizeRequest> = {
        id: crypto.randomUUID(),
        timestamp: Date.now(),
        filters: enabledFilters,
        request: payload,
        resultSummary: summary,
      };
      persistHistory([entry, ...history].slice(0, HISTORY_LIMIT));
      toast({ title: 'Optimization complete', description: summary || 'Success' });
    } catch (error: any) {
      toast({
        title: 'Optimization failed',
        description: error?.message || 'Unknown error',
        variant: 'destructive',
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Card className="p-4">
      <div className="flex items-center justify-between mb-4 border-b border-border pb-3">
        <h3 className="text-sm font-semibold tracking-tight text-foreground">Trade Optimization</h3>
        <div className="flex items-center gap-2">
          <Button
            onClick={runOptimize}
            disabled={isSubmitting}
            className="bg-primary text-primary-foreground hover:bg-primary/90 disabled:opacity-50 text-xs font-medium px-4 py-1.5 transition-colors"
          >
            {isSubmitting ? 'Running...' : 'Run Optimization'}
          </Button>
        </div>
      </div>

      <div className="grid gap-4 lg:grid-cols-[1.4fr_0.6fr]">
        <div className="space-y-4">
          <Card className="p-3">
            <FilterComposer 
              ref={filterComposerRef}
              onAddFilter={handleAddFilter} 
              disabled={isSubmitting} 
            />
          </Card>

          <Card className="p-3 space-y-3">
            <div className="flex items-center justify-between">
              <div className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">Active Filters</div>
              <Button
                variant="outline"
                size="sm"
                onClick={handleClearFilters}
                disabled={filters.length === 0 || isSubmitting}
                className="border-border bg-transparent text-muted-foreground hover:bg-accent hover:text-red-600 dark:hover:text-red-400 text-xs px-3 py-1 transition-colors disabled:opacity-50"
              >
                Clear All
              </Button>
              </div>
            <FilterList 
              filters={filters} 
              onToggle={handleToggleFilter} 
              onRemove={handleRemoveFilter} 
              onEdit={handleEditFilter}
              disabled={isSubmitting} 
            />
          </Card>
            </div>

        <div className="space-y-4">
          <Card className="p-3">
        <div className="flex items-center gap-3 flex-wrap">
          <div className="text-xs text-muted-foreground">Type</div>
              <select
                className="px-2 py-1 rounded-lg border border-input bg-card text-foreground text-xs transition-colors"
                value={tradeType ?? ''}
                onChange={event => setTradeType(event.target.value || undefined)}
                disabled={isSubmitting}
              >
            <option value="">Any</option>
            <option value="Paper">Paper</option>
            <option value="Live">Live</option>
          </select>
          <div className="text-xs text-muted-foreground">Status</div>
              <select
                className="px-2 py-1 rounded-lg border border-input bg-card text-foreground text-xs transition-colors"
                value={tradeStatus ?? ''}
                onChange={event => setTradeStatus(event.target.value || undefined)}
                disabled={isSubmitting}
              >
            <option value="">Any</option>
            <option value="Open">Open</option>
            <option value="Closed">Closed</option>
          </select>
                      </div>
        </Card>

          <Card className="p-3">
          <div className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground mb-3">Execution History</div>
          {history.length === 0 ? (
            <div className="text-xs text-muted-foreground/60">No execution history</div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow className="border-border hover:bg-muted/50">
                  <TableHead className="text-xs font-medium text-muted-foreground">Timestamp</TableHead>
                  <TableHead className="text-xs font-medium text-muted-foreground">Results</TableHead>
                  <TableHead className="text-xs font-medium text-muted-foreground">Filters</TableHead>
                  <TableHead className="w-40 text-xs font-medium text-muted-foreground">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                  {history.map(entry => (
                    <TableRow key={entry.id} className="border-border hover:bg-muted/50">
                      <TableCell className="text-xs text-foreground tabular-nums">{new Date(entry.timestamp).toLocaleString()}</TableCell>
                      <TableCell className="text-xs text-foreground tabular-nums">{entry.resultSummary ?? '-'}</TableCell>
                    <TableCell className="font-mono text-xs text-muted-foreground">
                        {entry.filters.map((filter, index) => (
                          <div key={`${entry.id}-${index}`}>{filter}</div>
                      ))}
                    </TableCell>
                    <TableCell>
                      <div className="flex gap-2">
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => loadFromHistory(entry)}
                            disabled={isSubmitting}
                            className="border-border bg-transparent text-muted-foreground hover:bg-accent hover:text-foreground text-xs px-2 py-1 transition-colors disabled:opacity-50"
                          >
                            Load
                          </Button>
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => deleteHistoryEntry(entry.id)}
                            disabled={isSubmitting}
                            className="border-border bg-transparent text-red-600 dark:text-red-400 hover:bg-accent hover:text-red-600 dark:hover:text-red-400 text-xs px-2 py-1 transition-colors disabled:opacity-50"
                          >
                            Delete
                          </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </Card>
        </div>
      </div>
    </Card>
  );
}

export default StrategyOptimizePanel;


