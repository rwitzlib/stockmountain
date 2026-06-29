import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { formatDateTime, formatDateTimeNoMinutes, formatDateNoTimezone } from '../../utils/dateFormatter';
import { formatCurrency } from '../../utils/formatters';
import { BacktestEntry } from '../../types/backtest';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../ui/table';
import { ArrowUpDown, ChevronDown, ChevronRight, Award, AlertTriangle } from 'lucide-react';
import { Badge } from '../ui/badge';

interface BacktestResultsTableProps {
  results: BacktestEntry[];
  sortConfig: {
    key: keyof BacktestEntry | null;
    direction: 'asc' | 'desc';
  };
  onSort: (key: keyof BacktestEntry) => void;
}

// Helper function to format duration seconds into readable format
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

export function BacktestResultsTable({ results, sortConfig, onSort }: BacktestResultsTableProps) {
  const navigate = useNavigate();
  const [expandedRows, setExpandedRows] = useState<Set<string>>(new Set());

  const handleRowClick = (result: BacktestEntry, e: React.MouseEvent) => {
    // Don't navigate if clicking on expand button
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

  // Calculate profit intensity for color coding (0-1 scale)
  const getProfitIntensity = (profit: number, maxProfit: number, minProfit: number): number => {
    if (maxProfit === minProfit) return 0.5;
    return Math.max(0, Math.min(1, (profit - minProfit) / (maxProfit - minProfit)));
  };

  // Get performance badge
  const getPerformanceBadge = (result: BacktestEntry, allResults: BacktestEntry[]): { label: string; color: string } | null => {
    if (result.status !== 'Completed') return null;
    
    const completedResults = allResults.filter(r => r.status === 'Completed');
    if (completedResults.length === 0) return null;

    const sortedByProfit = [...completedResults].sort((a, b) => (b.highProfit || 0) - (a.highProfit || 0));
    const top10Percent = Math.max(1, Math.floor(completedResults.length * 0.1));
    
    const rank = sortedByProfit.findIndex(r => r.id === result.id);
    if (rank < top10Percent) {
      return { label: 'Top Performer', color: 'bg-amber-100 dark:bg-amber-950 text-amber-700 dark:text-amber-400 border-amber-300 dark:border-amber-800' };
    }

    // High risk if profit/duration ratio is very high (might be unstable)
    const profitPerSecond = (result.highProfit || 0) / (result.durationSeconds || 1);
    const avgProfitPerSecond = completedResults.reduce((sum, r) => sum + ((r.highProfit || 0) / (r.durationSeconds || 1)), 0) / completedResults.length;
    if (profitPerSecond > avgProfitPerSecond * 3) {
      return { label: 'High Risk', color: 'bg-orange-100 dark:bg-orange-950 text-orange-700 dark:text-orange-400 border-orange-300 dark:border-orange-800' };
    }

    return null;
  };

  // Calculate max and min profits for intensity scaling
  const completedResults = results.filter(r => r.status === 'Completed');
  const profits = completedResults.map(r => r.highProfit || 0);
  const maxProfit = profits.length > 0 ? Math.max(...profits) : 0;
  const minProfit = profits.length > 0 ? Math.min(...profits) : 0;

  return (
    <div className="table-container bg-card border border-border">
      <Table>
        <TableHeader>
          <TableRow className="border-border hover:bg-muted/50">
            <TableHead 
              className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground cursor-pointer hover:text-primary transition-colors"
              onClick={() => onSort('createdAt')}
            >
              Created
              <ArrowUpDown className="ml-1 h-3 w-3 inline" />
            </TableHead>
            <TableHead 
              className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground text-center cursor-pointer hover:text-primary transition-colors"
              onClick={() => onSort('status')}
            >
              Status
              <ArrowUpDown className="ml-1 h-3 w-3 inline" />
            </TableHead>
            <TableHead 
              className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground cursor-pointer hover:text-primary transition-colors"
              onClick={() => onSort('start')}
            >
              Start
              <ArrowUpDown className="ml-1 h-3 w-3 inline" />
            </TableHead>
            <TableHead 
              className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground cursor-pointer hover:text-primary transition-colors"
              onClick={() => onSort('end')}
            >
              End
              <ArrowUpDown className="ml-1 h-3 w-3 inline" />
            </TableHead>
            <TableHead 
              className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground text-center cursor-pointer hover:text-primary transition-colors"
              onClick={() => onSort('durationSeconds')}
            >
              Duration
              <ArrowUpDown className="ml-1 h-3 w-3 inline" />
            </TableHead>
            <TableHead 
              className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground text-right cursor-pointer hover:text-primary transition-colors"
              onClick={() => onSort('holdProfit')}
            >
              Hold P/L
              <ArrowUpDown className="ml-1 h-3 w-3 inline" />
            </TableHead>
            <TableHead 
              className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground text-right cursor-pointer hover:text-primary transition-colors"
              onClick={() => onSort('highProfit')}
            >
              High P/L
              <ArrowUpDown className="ml-1 h-3 w-3 inline" />
            </TableHead>
            <TableHead 
              className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground text-right cursor-pointer hover:text-primary transition-colors"
            >
              Conditional P/L
            </TableHead>
            <TableHead 
              className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground text-center cursor-pointer hover:text-primary transition-colors"
              title="Win Ratio (Hold Strategy): Percentage of winning trades - realistic performance"
            >
              Win Ratio
            </TableHead>
            <TableHead 
              className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground text-center cursor-pointer hover:text-primary transition-colors"
              title="Profit Factor (Hold Strategy): Ratio of winning profits to losing losses - realistic performance"
            >
              Profit Factor
            </TableHead>
            <TableHead 
              className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground text-center cursor-pointer hover:text-primary transition-colors"
              title="Total number of trades executed (Hold Strategy)"
            >
              Trades
            </TableHead>
            <TableHead 
              className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground text-center cursor-pointer hover:text-primary transition-colors"
              title="Max Drawdown (Hold Strategy): Largest peak-to-trough decline - realistic performance"
            >
              Max DD
            </TableHead>
            <TableHead 
              className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground text-right cursor-pointer hover:text-primary transition-colors"
              onClick={() => onSort('creditsUsed')}
            >
              Credits
              <ArrowUpDown className="ml-1 h-3 w-3 inline" />
            </TableHead>
            <TableHead className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground text-center w-12">
              Details
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {results.map((result) => {
            const isExpanded = expandedRows.has(result.id);
            const performanceBadge = getPerformanceBadge(result, results);
            const conditionalProfit = (result as any).conditionalProfit ?? null;
            
            // Calculate profit intensity for color coding
            let profitIntensity = 0.5;
            if (result.status === 'Completed' && maxProfit !== minProfit) {
              profitIntensity = getProfitIntensity(result.highProfit || 0, maxProfit, minProfit);
            }

            // Determine background color intensity for profit cells
            const getProfitBgColor = (profit: number, isPositive: boolean) => {
              if (result.status !== 'Completed') return '';
              // Use opacity-based approach instead of dynamic class names
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
              <>
                <TableRow 
                  key={result.id} 
                  className="border-border hover:bg-muted/50 hover:border-primary cursor-pointer transition-all"
                  onClick={(e) => handleRowClick(result, e)}
                >
                  <TableCell className="font-mono text-xs text-muted-foreground">{formatDateTime(new Date(result.createdAt))}</TableCell>
                  <TableCell className="text-center">
                    <div className="flex items-center justify-center gap-1.5">
                      <span className={`px-2 py-0.5 text-[9px] font-mono uppercase border ${
                        result.status === 'Completed' 
                          ? 'bg-green-100 dark:bg-green-950 text-green-700 dark:text-green-400 border-green-300 dark:border-green-800' :
                        result.status === 'InProgress' 
                          ? 'bg-yellow-100 dark:bg-yellow-950 text-yellow-700 dark:text-yellow-400 border-yellow-300 dark:border-yellow-800 animate-pulse' :
                          'bg-red-100 dark:bg-red-950 text-red-700 dark:text-red-400 border-red-300 dark:border-red-800'
                      }`}>
                        {result.status}
                      </span>
                      {performanceBadge && (
                        <Badge className={`text-[8px] font-mono uppercase border ${performanceBadge.color}`}>
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
                  <TableCell className="font-mono text-xs text-primary dark:text-cyan-400">{formatDateNoTimezone(result.start)}</TableCell>
                  <TableCell className="font-mono text-xs text-primary dark:text-cyan-400">{formatDateNoTimezone(result.end)}</TableCell>
                  <TableCell className="text-center font-mono text-xs text-muted-foreground">{formatDuration(result.durationSeconds)}</TableCell>
                  <TableCell className={`text-right ${getProfitBgColor(result.holdProfit, result.holdProfit >= 0)}`}>
                    <span className={`font-mono text-xs font-bold ${
                      result.holdProfit >= 0 
                        ? 'text-green-600 dark:text-green-400' 
                        : 'text-red-600 dark:text-red-400'
                    }`}>
                      {formatCurrency(result.holdProfit)}
                    </span>
                  </TableCell>
                  <TableCell className={`text-right ${getProfitBgColor(result.highProfit, result.highProfit >= 0)}`}>
                    <span className={`font-mono text-xs font-bold ${
                      result.highProfit >= 0 
                        ? 'text-green-600 dark:text-green-400' 
                        : 'text-red-600 dark:text-red-400'
                    }`}>
                      {formatCurrency(result.highProfit)}
                    </span>
                  </TableCell>
                  <TableCell className="text-right">
                    {conditionalProfit !== null ? (
                      <span className={`font-mono text-xs font-bold ${
                        conditionalProfit >= 0 
                          ? 'text-green-600 dark:text-green-400' 
                          : 'text-red-600 dark:text-red-400'
                      }`}>
                        {formatCurrency(conditionalProfit)}
                      </span>
                    ) : (
                      <span className="font-mono text-xs text-muted-foreground">—</span>
                    )}
                  </TableCell>
                  <TableCell className="text-center">
                    {result.holdStats ? (
                      <span className="font-mono text-xs" title={`Win Ratio (Hold): ${(result.holdStats.winRatio * 100).toFixed(1)}% - Realistic performance`}>
                        {(result.holdStats.winRatio * 100).toFixed(1)}%
                      </span>
                    ) : (
                      <span className="font-mono text-xs text-muted-foreground">—</span>
                    )}
                  </TableCell>
                  <TableCell className="text-center">
                    {result.holdStats ? (
                      <span 
                        className={`font-mono text-xs font-bold ${
                          result.holdStats.profitFactor > 1.5 
                            ? 'text-green-600 dark:text-green-400' 
                            : result.holdStats.profitFactor > 1.0
                            ? 'text-yellow-600 dark:text-yellow-400'
                            : 'text-red-600 dark:text-red-400'
                        }`}
                        title={`Profit Factor (Hold): ${result.holdStats.profitFactor.toFixed(2)} - Realistic performance`}
                      >
                        {result.holdStats.profitFactor.toFixed(2)}
                      </span>
                    ) : (
                      <span className="font-mono text-xs text-muted-foreground">—</span>
                    )}
                  </TableCell>
                  <TableCell className="text-center">
                    {result.holdStats ? (
                      <span 
                        className="font-mono text-xs"
                        title={`Total Trades (Hold): ${result.holdStats.totalTradesTaken}`}
                      >
                        {result.holdStats.totalTradesTaken}
                      </span>
                    ) : (
                      <span className="font-mono text-xs text-muted-foreground">—</span>
                    )}
                  </TableCell>
                  <TableCell className="text-center">
                    {result.holdStats ? (
                      <span 
                        className={`font-mono text-xs ${
                          result.holdStats.maxDrawdown < 0.1 
                            ? 'text-green-600 dark:text-green-400' 
                            : result.holdStats.maxDrawdown < 0.2
                            ? 'text-yellow-600 dark:text-yellow-400'
                            : 'text-red-600 dark:text-red-400'
                        }`}
                        title={`Max Drawdown (Hold): ${(result.holdStats.maxDrawdown * 100).toFixed(2)}% - Realistic performance`}
                      >
                        {(result.holdStats.maxDrawdown * 100).toFixed(1)}%
                      </span>
                    ) : (
                      <span className="font-mono text-xs text-muted-foreground">—</span>
                    )}
                  </TableCell>
                  <TableCell className="text-right font-mono text-xs text-purple-600 dark:text-purple-400">{result.creditsUsed}</TableCell>
                  <TableCell className="text-center">
                    <button
                      onClick={(e) => toggleExpand(result.id, e)}
                      className="expand-button p-1 hover:bg-muted rounded transition-colors"
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
                  <TableRow key={`${result.id}-details`} className="border-border bg-muted/20">
                    <TableCell colSpan={10} className="p-4">
                      <div className="space-y-3 font-mono text-xs">
                        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                          <div>
                            <div className="text-[10px] uppercase tracking-wider text-muted-foreground mb-1">Starting Balance</div>
                            <div className="text-primary dark:text-cyan-400">
                              {formatCurrency(result.requestDetails?.positionInfo?.startingBalance || 0)}
                            </div>
                          </div>
                          <div>
                            <div className="text-[10px] uppercase tracking-wider text-muted-foreground mb-1">Max Positions</div>
                            <div className="text-primary dark:text-cyan-400">
                              {result.requestDetails?.positionInfo?.maxConcurrentPositions || 'N/A'}
                            </div>
                          </div>
                          <div>
                            <div className="text-[10px] uppercase tracking-wider text-muted-foreground mb-1">Position Size</div>
                            <div className="text-primary dark:text-cyan-400">
                              {result.requestDetails?.positionInfo?.positionSize || 'N/A'}
                            </div>
                          </div>
                          <div>
                            <div className="text-[10px] uppercase tracking-wider text-muted-foreground mb-1">Credits Used</div>
                            <div className="text-purple-600 dark:text-purple-400">
                              {result.creditsUsed}
                            </div>
                          </div>
                        </div>
                        {result.requestDetails?.exitInfo && (
                          <div className="border-t border-border pt-3">
                            <div className="text-[10px] uppercase tracking-wider text-muted-foreground mb-2">Exit Settings</div>
                            <div className="grid grid-cols-1 md:grid-cols-3 gap-2 text-xs">
                              {result.requestDetails.exitInfo.stopLoss && (
                                <div className="bg-card border border-border p-2 rounded">
                                  <div className="text-muted-foreground">Stop Loss:</div>
                                  <div className="text-foreground">
                                    {result.requestDetails.exitInfo.stopLoss.type} @ {result.requestDetails.exitInfo.stopLoss.value}
                                  </div>
                                </div>
                              )}
                              {result.requestDetails.exitInfo.profitTarget && (
                                <div className="bg-card border border-border p-2 rounded">
                                  <div className="text-muted-foreground">Profit Target:</div>
                                  <div className="text-foreground">
                                    {result.requestDetails.exitInfo.profitTarget.type} @ {result.requestDetails.exitInfo.profitTarget.value}
                                  </div>
                                </div>
                              )}
                              {result.requestDetails.exitInfo.timedExit && (
                                <div className="bg-card border border-border p-2 rounded">
                                  <div className="text-muted-foreground">Timed Exit:</div>
                                  <div className="text-foreground">
                                    {result.requestDetails.exitInfo.timedExit.timeframe.multiplier} {result.requestDetails.exitInfo.timedExit.timeframe.timespan}
                                  </div>
                                </div>
                              )}
                            </div>
                          </div>
                        )}
                        {result.requestDetails?.entryInfo?.filters && result.requestDetails.entryInfo.filters.length > 0 && (
                          <div className="border-t border-border pt-3">
                            <div className="text-[10px] uppercase tracking-wider text-muted-foreground mb-2">
                              Entry Filters ({result.requestDetails.entryInfo.filters.length})
                            </div>
                            <div className="flex flex-wrap gap-1">
                              {result.requestDetails.entryInfo.filters.map((filter, idx) => (
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
              </>
            );
          })}
        </TableBody>
      </Table>
    </div>
  );
} 