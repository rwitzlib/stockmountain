import { useState, Fragment } from 'react';
import { useNavigate } from 'react-router-dom';
import { formatDateTime, formatDateNoTimezone } from '../../utils/dateFormatter';
import { formatCurrency } from '../../utils/formatters';
import { BacktestEntry } from '../../types/backtest';
import { getBacktestRequestInfo } from '../../utils/backtestRequest';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../ui/table';
import { ArrowUpDown, ChevronDown, ChevronRight, Award, AlertTriangle, Filter } from 'lucide-react';
import { Badge } from '../ui/badge';
import { Button } from '../ui/button';

interface BacktestResultsTableProps {
  results: BacktestEntry[];
  sortConfig: {
    key: keyof BacktestEntry | null;
    direction: 'asc' | 'desc';
  };
  onSort: (key: keyof BacktestEntry) => void;
  hasActiveFilters?: boolean;
  onClearFilters?: () => void;
  totalCount?: number;
}

const formatDuration = (seconds: number): string => {
  if (seconds < 60) {
    return `${seconds}s`;
  } else if (seconds < 3600) {
    const minutes = Math.floor(seconds / 60);
    const remainingSeconds = seconds % 60;
    return remainingSeconds > 0 ? `${minutes}m ${remainingSeconds}s` : `${minutes}m`;
  } else if (seconds < 86400) {
    const hours = Math.floor(seconds / 3600);
    const remainingMinutes = Math.floor((seconds % 3600) / 60);
    return remainingMinutes > 0 ? `${hours}h ${remainingMinutes}m` : `${hours}h`;
  } else {
    const days = Math.floor(seconds / 86400);
    const remainingHours = Math.floor((seconds % 86400) / 3600);
    return remainingHours > 0 ? `${days}d ${remainingHours}h` : `${days}d`;
  }
};

const truncateFilter = (filter: string, maxLen = 36): string => {
  if (filter.length <= maxLen) return filter;
  return `${filter.slice(0, maxLen - 1)}…`;
};

export function BacktestResultsTable({
  results,
  sortConfig,
  onSort,
  hasActiveFilters = false,
  onClearFilters,
  totalCount = 0,
}: BacktestResultsTableProps) {
  const navigate = useNavigate();
  const [expandedRows, setExpandedRows] = useState<Set<string>>(new Set());

  const handleRowClick = (result: BacktestEntry, e: React.MouseEvent) => {
    if ((e.target as HTMLElement).closest('.expand-button')) {
      return;
    }
    navigate(`/backtest/${result.id}`);
  };

  const toggleExpand = (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    setExpandedRows(prev => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  const getProfitIntensity = (profit: number, maxProfit: number, minProfit: number): number => {
    if (maxProfit === minProfit) return 0.5;
    return Math.max(0, Math.min(1, (profit - minProfit) / (maxProfit - minProfit)));
  };

  const getPerformanceBadge = (result: BacktestEntry, allResults: BacktestEntry[]): { label: string; color: string } | null => {
    if (result.status !== 'Completed') return null;

    const completedResults = allResults.filter(r => r.status === 'Completed');
    if (completedResults.length === 0) return null;

    const sortedByProfit = [...completedResults].sort((a, b) => (b.holdProfit || 0) - (a.holdProfit || 0));
    const top10Percent = Math.max(1, Math.floor(completedResults.length * 0.1));

    const rank = sortedByProfit.findIndex(r => r.id === result.id);
    if (rank < top10Percent) {
      return { label: 'Top Performer', color: 'bg-amber-500/10 text-amber-600 dark:text-amber-400 border-transparent' };
    }

    const profitPerSecond = (result.holdProfit || 0) / (result.durationSeconds || 1);
    const avgProfitPerSecond = completedResults.reduce((sum, r) => sum + ((r.holdProfit || 0) / (r.durationSeconds || 1)), 0) / completedResults.length;
    if (profitPerSecond > avgProfitPerSecond * 3) {
      return { label: 'High Risk', color: 'bg-orange-500/10 text-orange-600 dark:text-orange-400 border-transparent' };
    }

    return null;
  };

  const completedResults = results.filter(r => r.status === 'Completed');
  const profits = completedResults.map(r => r.holdProfit || 0);
  const maxProfit = profits.length > 0 ? Math.max(...profits) : 0;
  const minProfit = profits.length > 0 ? Math.min(...profits) : 0;

  if (results.length === 0) {
    return (
      <div className="rounded-xl border border-border/80 bg-card p-10 text-center space-y-3">
        <Filter className="h-8 w-8 text-muted-foreground mx-auto opacity-50" />
        <p className="text-sm text-foreground">
          {hasActiveFilters ? 'No backtests match your filters' : 'No backtests yet'}
        </p>
        <p className="text-xs text-muted-foreground">
          {hasActiveFilters
            ? `Showing 0 of ${totalCount}. Try adjusting or clearing filters.`
            : 'Create a backtest to see results here.'}
        </p>
        {hasActiveFilters && onClearFilters && (
          <Button
            onClick={onClearFilters}
            variant="outline"
            size="sm"
            className="text-xs mt-2"
          >
            Clear Filters
          </Button>
        )}
      </div>
    );
  }

  return (
    <div className="table-container rounded-xl border border-border/80 bg-card overflow-x-auto">
      <Table>
        <TableHeader>
          <TableRow className="border-border hover:bg-muted/50">
            <TableHead
              className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground cursor-pointer hover:text-foreground transition-colors"
              onClick={() => onSort('createdAt')}
            >
              Created
              <ArrowUpDown className="ml-1 h-3 w-3 inline" />
            </TableHead>
            <TableHead
              className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground text-center cursor-pointer hover:text-foreground transition-colors"
              onClick={() => onSort('status')}
            >
              Status
              <ArrowUpDown className="ml-1 h-3 w-3 inline" />
            </TableHead>
            <TableHead className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground min-w-[140px]">
              Entry Filters
            </TableHead>
            <TableHead
              className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground cursor-pointer hover:text-foreground transition-colors"
              onClick={() => onSort('start')}
            >
              Period
              <ArrowUpDown className="ml-1 h-3 w-3 inline" />
            </TableHead>
            <TableHead
              className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground text-center cursor-pointer hover:text-foreground transition-colors"
              onClick={() => onSort('durationSeconds')}
            >
              Duration
              <ArrowUpDown className="ml-1 h-3 w-3 inline" />
            </TableHead>
            <TableHead
              className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground text-right cursor-pointer hover:text-foreground transition-colors"
              onClick={() => onSort('holdProfit')}
            >
              Hold P/L
              <ArrowUpDown className="ml-1 h-3 w-3 inline" />
            </TableHead>
            <TableHead
              className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground text-right cursor-pointer hover:text-foreground transition-colors"
              onClick={() => onSort('highProfit')}
            >
              High P/L
              <ArrowUpDown className="ml-1 h-3 w-3 inline" />
            </TableHead>
            <TableHead
              className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground text-right cursor-pointer hover:text-foreground transition-colors"
              onClick={() => onSort('creditsUsed')}
            >
              Credits
              <ArrowUpDown className="ml-1 h-3 w-3 inline" />
            </TableHead>
            <TableHead className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground text-center w-12">
              Details
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {results.map((result) => {
            const isExpanded = expandedRows.has(result.id);
            const performanceBadge = getPerformanceBadge(result, results);
            const requestInfo = getBacktestRequestInfo(result);
            const filters = requestInfo.filters || [];
            const visibleFilters = filters.slice(0, 2);
            const extraFilterCount = filters.length - visibleFilters.length;

            let profitIntensity = 0.5;
            if (result.status === 'Completed' && maxProfit !== minProfit) {
              profitIntensity = getProfitIntensity(result.holdProfit || 0, maxProfit, minProfit);
            }

            const getProfitBgColor = (isPositive: boolean) => {
              if (result.status !== 'Completed') return '';
              if (isPositive && profitIntensity > 0.7) {
                return 'bg-green-500/10 dark:bg-green-500/20';
              } else if (isPositive && profitIntensity > 0.4) {
                return 'bg-green-500/5 dark:bg-green-500/10';
              } else if (!isPositive && profitIntensity < 0.3) {
                return 'bg-red-500/10 dark:bg-red-500/20';
              } else if (!isPositive && profitIntensity < 0.6) {
                return 'bg-red-500/5 dark:bg-red-500/10';
              }
              return '';
            };

            return (
              <Fragment key={result.id}>
                <TableRow
                  className="border-border hover:bg-accent/40 cursor-pointer transition-colors"
                  onClick={(e) => handleRowClick(result, e)}
                >
                  <TableCell className="text-xs text-muted-foreground tabular-nums whitespace-nowrap">
                    {formatDateTime(new Date(result.createdAt))}
                  </TableCell>
                  <TableCell className="text-center">
                    <div className="flex items-center justify-center gap-1.5 flex-wrap">
                      <span className={`rounded-full px-2.5 py-0.5 text-[11px] font-semibold ${
                        result.status === 'Completed'
                          ? 'bg-green-500/10 text-green-600 dark:text-green-400' :
                        result.status === 'InProgress'
                          ? 'bg-yellow-500/10 text-yellow-600 dark:text-yellow-400' :
                          'bg-red-500/10 text-red-600 dark:text-red-400'
                      }`}>
                        {result.status}
                      </span>
                      {performanceBadge && (
                        <Badge className={`rounded-full px-2.5 py-0.5 text-[10px] font-medium ${performanceBadge.color}`}>
                          {performanceBadge.label === 'Top Performer' && <Award className="h-2.5 w-2.5 mr-0.5" />}
                          {performanceBadge.label === 'High Risk' && <AlertTriangle className="h-2.5 w-2.5 mr-0.5" />}
                          {performanceBadge.label}
                        </Badge>
                      )}
                    </div>
                    {result.status === 'InProgress' && (
                      <div className="mt-1 w-full bg-muted rounded-full h-1">
                        <div
                          className="bg-yellow-600 dark:bg-yellow-400 h-1 rounded-full transition-all duration-300"
                          style={{ width: `${Math.min(90, (result.durationSeconds || 0) / 10)}%` }}
                        />
                      </div>
                    )}
                  </TableCell>
                  <TableCell>
                    {filters.length > 0 ? (
                      <div className="flex flex-wrap gap-1 max-w-[220px]">
                        {visibleFilters.map((filter, idx) => (
                          <Badge
                            key={idx}
                            variant="outline"
                            className="text-[9px] font-mono max-w-full truncate"
                            title={filter}
                          >
                            {truncateFilter(filter)}
                          </Badge>
                        ))}
                        {extraFilterCount > 0 && (
                          <Badge variant="outline" className="text-[9px] font-mono text-muted-foreground">
                            +{extraFilterCount}
                          </Badge>
                        )}
                      </div>
                    ) : (
                      <span className="text-xs text-muted-foreground">—</span>
                    )}
                  </TableCell>
                  <TableCell className="text-xs text-foreground tabular-nums whitespace-nowrap">
                    {formatDateNoTimezone(result.start)}
                    {result.start !== result.end && (
                      <span className="text-muted-foreground"> → {formatDateNoTimezone(result.end)}</span>
                    )}
                  </TableCell>
                  <TableCell className="text-center text-xs text-muted-foreground tabular-nums">
                    {formatDuration(result.durationSeconds)}
                  </TableCell>
                  <TableCell className={`text-right ${getProfitBgColor(result.holdProfit >= 0)}`}>
                    <span className={`text-xs font-semibold tabular-nums ${
                      result.holdProfit >= 0
                        ? 'text-green-600 dark:text-green-400'
                        : 'text-red-600 dark:text-red-400'
                    }`}>
                      {formatCurrency(result.holdProfit)}
                    </span>
                  </TableCell>
                  <TableCell className={`text-right ${getProfitBgColor(result.highProfit >= 0)}`}>
                    <span className={`text-xs font-semibold tabular-nums ${
                      result.highProfit >= 0
                        ? 'text-green-600 dark:text-green-400'
                        : 'text-red-600 dark:text-red-400'
                    }`}>
                      {formatCurrency(result.highProfit)}
                    </span>
                  </TableCell>
                  <TableCell className="text-right text-xs text-foreground tabular-nums">
                    {typeof result.creditsUsed === 'number' ? result.creditsUsed.toFixed(1) : result.creditsUsed}
                  </TableCell>
                  <TableCell className="text-center">
                    <button
                      onClick={(e) => toggleExpand(result.id, e)}
                      className="expand-button p-1 hover:bg-accent rounded-md transition-colors"
                      aria-label={isExpanded ? 'Collapse details' : 'Expand details'}
                    >
                      {isExpanded ? (
                        <ChevronDown className="h-4 w-4 text-muted-foreground" />
                      ) : (
                        <ChevronRight className="h-4 w-4 text-muted-foreground" />
                      )}
                    </button>
                  </TableCell>
                </TableRow>
                {isExpanded && (
                  <TableRow className="border-border bg-muted/20">
                    <TableCell colSpan={9} className="p-4">
                      <div className="space-y-3 text-xs tabular-nums">
                        {/* Stats that used to be table columns */}
                        {(result.holdStats || result.conditionalProfit != null) && (
                          <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
                            {result.conditionalProfit != null && (
                              <div>
                                <div className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground mb-1">Conditional P/L</div>
                                <div className={`font-semibold ${
                                  result.conditionalProfit >= 0
                                    ? 'text-green-600 dark:text-green-400'
                                    : 'text-red-600 dark:text-red-400'
                                }`}>
                                  {formatCurrency(result.conditionalProfit)}
                                </div>
                              </div>
                            )}
                            {result.holdStats && (
                              <>
                                <div>
                                  <div className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground mb-1">Win Ratio</div>
                                  <div className="text-foreground">
                                    {(result.holdStats.winRatio * 100).toFixed(1)}%
                                  </div>
                                </div>
                                <div>
                                  <div className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground mb-1">Profit Factor</div>
                                  <div className={`font-semibold ${
                                    result.holdStats.profitFactor > 1.5
                                      ? 'text-green-600 dark:text-green-400'
                                      : result.holdStats.profitFactor > 1.0
                                      ? 'text-yellow-600 dark:text-yellow-400'
                                      : 'text-red-600 dark:text-red-400'
                                  }`}>
                                    {result.holdStats.profitFactor.toFixed(2)}
                                  </div>
                                </div>
                                <div>
                                  <div className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground mb-1">Trades</div>
                                  <div className="text-foreground">{result.holdStats.totalTradesTaken}</div>
                                </div>
                                <div>
                                  <div className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground mb-1">Max Drawdown</div>
                                  <div className={`${
                                    result.holdStats.maxDrawdown < 0.1
                                      ? 'text-green-600 dark:text-green-400'
                                      : result.holdStats.maxDrawdown < 0.2
                                      ? 'text-yellow-600 dark:text-yellow-400'
                                      : 'text-red-600 dark:text-red-400'
                                  }`}>
                                    {(result.holdStats.maxDrawdown * 100).toFixed(1)}%
                                  </div>
                                </div>
                              </>
                            )}
                          </div>
                        )}

                        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                          <div>
                            <div className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground mb-1">Starting Balance</div>
                            <div className="text-foreground tabular-nums">
                              {formatCurrency(requestInfo.positionInfo.startingBalance || 0)}
                            </div>
                          </div>
                          <div>
                            <div className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground mb-1">Max Positions</div>
                            <div className="text-foreground tabular-nums">
                              {requestInfo.positionInfo.maxConcurrentPositions || 'N/A'}
                            </div>
                          </div>
                          <div>
                            <div className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground mb-1">Position Size</div>
                            <div className="text-foreground tabular-nums">
                              {requestInfo.positionInfo.positionSize || 'N/A'}
                            </div>
                          </div>
                          <div>
                            <div className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground mb-1">Credits Used</div>
                            <div className="text-foreground tabular-nums">
                              {result.creditsUsed}
                            </div>
                          </div>
                        </div>

                        {requestInfo.exitInfo && (requestInfo.exitInfo.stopLoss || requestInfo.exitInfo.profitTarget || requestInfo.exitInfo.timedExit) && (
                          <div className="border-t border-border pt-3">
                            <div className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground mb-2">Exit Settings</div>
                            <div className="grid grid-cols-1 md:grid-cols-3 gap-2 text-xs">
                              {requestInfo.exitInfo.stopLoss && (
                                <div className="rounded-lg border border-border/60 bg-muted/30 p-2">
                                  <div className="text-muted-foreground">Stop Loss:</div>
                                  <div className="text-foreground">
                                    {(requestInfo.exitInfo.stopLoss.type || requestInfo.exitInfo.stopLoss.priceActionType || 'value')} @ {requestInfo.exitInfo.stopLoss.value}
                                  </div>
                                </div>
                              )}
                              {requestInfo.exitInfo.profitTarget && (
                                <div className="rounded-lg border border-border/60 bg-muted/30 p-2">
                                  <div className="text-muted-foreground">Profit Target:</div>
                                  <div className="text-foreground">
                                    {(requestInfo.exitInfo.profitTarget.type || requestInfo.exitInfo.profitTarget.priceActionType || 'value')} @ {requestInfo.exitInfo.profitTarget.value}
                                  </div>
                                </div>
                              )}
                              {requestInfo.exitInfo.timedExit && (
                                <div className="rounded-lg border border-border/60 bg-muted/30 p-2">
                                  <div className="text-muted-foreground">Timed Exit:</div>
                                  <div className="text-foreground">
                                    {requestInfo.exitInfo.timedExit.timeframe.multiplier}
                                    {requestInfo.exitInfo.timedExit.timeframe.timespan
                                      ? ` ${requestInfo.exitInfo.timedExit.timeframe.timespan}`
                                      : ' bars'}
                                  </div>
                                </div>
                              )}
                            </div>
                          </div>
                        )}

                        {filters.length > 0 && (
                          <div className="border-t border-border pt-3">
                            <div className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground mb-2">
                              Entry Filters ({filters.length})
                            </div>
                            <div className="flex flex-wrap gap-1">
                              {filters.map((filter, idx) => (
                                <Badge key={idx} variant="outline" className="text-[9px] font-mono">
                                  {filter}
                                </Badge>
                              ))}
                            </div>
                          </div>
                        )}
                      </div>
                    </TableCell>
                  </TableRow>
                )}
              </Fragment>
            );
          })}
        </TableBody>
      </Table>
    </div>
  );
}
