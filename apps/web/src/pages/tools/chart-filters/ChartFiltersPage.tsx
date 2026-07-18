import { useEffect, useMemo, useRef, useState, useCallback } from 'react';
import { Card } from '../../../components/ui/card';
import { Button } from '../../../components/ui/button';
import { FilterComposer } from '../../../components/filters/FilterComposer';
import { FilterList } from '../../../components/filters/FilterList';
import type { FilterItem } from '../../../types/filters';
import { StockChart } from '../../../components/charts/stock/StockChart';
import { ChartHeader } from '../../../components/charts/stock/ChartHeader';
import type { BarData, IndicatorSetup, IndicatorConfig } from '../../../types/tools';
import { Input } from '../../../components/ui/input';
import { toolsApi, type ChartFilterTimespan } from '../../../api/toolsApi';
import { fetchMarketData } from '../../../services/massive';
import { toast } from '../../../hooks/use-toast';

const createFilter = (expression: string): FilterItem => ({
  id: crypto.randomUUID(),
  enabled: true,
  expression,
});

export function ChartFiltersPage() {
  const [filters, setFilters] = useState<FilterItem[]>([]);
  const [symbol, setSymbol] = useState('SPY');
  const [timespan, setTimespan] = useState<ChartFilterTimespan>('minute');
  const [multiplier, setMultiplier] = useState(1);
  const [dateRange, setDateRange] = useState<{ from: string; to: string }>(() => {
    const to = new Date();
    const from = new Date(to.getTime() - 4 * 24 * 60 * 60 * 1000);
    return {
      from: from.toISOString().split('T')[0],
      to: to.toISOString().split('T')[0],
    };
  });
  const [isLoadingData, setIsLoadingData] = useState(false);
  const [isScanning, setIsScanning] = useState(false);
  const [matches, setMatches] = useState<number[]>([]);
  const [chartData, setChartData] = useState<BarData[]>([]);
  const [indicators, setIndicators] = useState<IndicatorSetup[]>([]);
  const [apiIndicators, setApiIndicators] = useState<IndicatorConfig[]>([]);
  const [chartDimensions, setChartDimensions] = useState({ width: 0, height: 0 });
  const chartWrapperRef = useRef<HTMLDivElement | null>(null);
  const headerRef = useRef<HTMLDivElement>(null);
  const hasInitialLoadRef = useRef(false);
  const shouldAutoScanRef = useRef(false);

  // Helper: Convert indicators to API format
  const convertIndicatorsToApiFormat = useCallback((indicators: IndicatorSetup[]): string[] => {
    return indicators.map(indicator => {
      const { type, params } = indicator;
      const paramValues = Object.values(params);

      switch (type) {
        case 'sma':
          return `sma(${paramValues.join(',')})`;
        case 'ema':
          return `ema(${paramValues.join(',')})`;
        case 'macd':
          return `macd(${paramValues.join(',')})`;
        case 'rsi':
          return `rsi(${paramValues.join(',')})`;
        default:
          return '';
      }
    }).filter(Boolean);
  }, []);

  // Helper: Parse timestamps from API response
  const parseTimestamps = useCallback((timestamps: Array<number | string>): number[] => {
    return timestamps
      .map(ts => (typeof ts === 'string' ? Date.parse(ts) : Number(ts)))
      .filter(ts => !Number.isNaN(ts));
  }, []);

  // Helper: Fetch indicators for current chart parameters
  const fetchIndicatorsForScan = useCallback(async () => {
    if (indicators.length === 0) {
      setApiIndicators([]);
      return;
    }

    try {
      const indicatorsStrings = convertIndicatorsToApiFormat(indicators);
      const indicatorData = await fetchMarketData({
        ticker: symbol.trim().toUpperCase(),
        multiplier,
        timespan,
        from: dateRange.from,
        to: dateRange.to,
        indicators: indicatorsStrings
      });
      setApiIndicators(indicatorData.indicators || []);
    } catch (error) {
      console.error('Failed to fetch indicators after scan:', error);
      // Don't fail the scan if indicators fail, just log it
    }
  }, [indicators, symbol, multiplier, timespan, dateRange, convertIndicatorsToApiFormat]);

  // Fetch chart data with indicators
  const fetchChartDataWithIndicators = useCallback(async () => {
    if (!symbol.trim() || !dateRange.from || !dateRange.to) return;

    try {
      setIsLoadingData(true);
      const indicatorsStrings = convertIndicatorsToApiFormat(indicators);
      
      const data = await fetchMarketData({
        ticker: symbol.trim().toUpperCase(),
        multiplier,
        timespan,
        from: dateRange.from,
        to: dateRange.to,
        indicators: indicatorsStrings
      });

      setChartData(data.results || []);
      setApiIndicators(data.indicators || []);
    } catch (error: any) {
      console.error('Error fetching chart data:', error);
      toast({
        title: 'Failed to load chart data',
        description: error?.message || 'Unable to fetch chart data. Please try again.',
        variant: 'destructive',
      });
    } finally {
      setIsLoadingData(false);
    }
  }, [symbol, multiplier, timespan, dateRange, indicators, convertIndicatorsToApiFormat]);

  useEffect(() => {
    const measureDimensions = () => {
      if (!chartWrapperRef.current || !headerRef.current) return;
      const { clientWidth, clientHeight } = chartWrapperRef.current;
      const headerHeight = headerRef.current.clientHeight;
      const height = Math.max(400, clientHeight - headerHeight);
      setChartDimensions({ width: clientWidth, height });
    };

    measureDimensions();
    window.addEventListener('resize', measureDimensions);
    
    // Use ResizeObserver for more accurate measurements
    const resizeObserver = new ResizeObserver(measureDimensions);
    if (chartWrapperRef.current) {
      resizeObserver.observe(chartWrapperRef.current);
    }
    if (headerRef.current) {
      resizeObserver.observe(headerRef.current);
    }

    return () => {
      window.removeEventListener('resize', measureDimensions);
      resizeObserver.disconnect();
    };
  }, []);

  const highlights = useMemo(() => {
    if (matches.length) {
      return matches.map((timestamp, index) => ({
        time: timestamp,
        label: `${index + 1}`,
      }));
    }

    return filters
      .filter(filter => filter.enabled)
      .map((_, index) => {
        const targetIndex = Math.min(chartData.length - 1, (index + 1) * 8);
        return {
          time: chartData[targetIndex]?.t ?? Date.now(),
          label: `${index + 1}`,
        };
      });
  }, [filters, matches, chartData]);

  const handleAddFilter = (expression: string) => {
    setFilters(prev => [createFilter(expression), ...prev]);
  };

  const handleToggleFilter = (id: string) => {
    setFilters(prev => prev.map(filter => (filter.id === id ? { ...filter, enabled: !filter.enabled } : filter)));
  };

  const handleRemoveFilter = (id: string) => {
    setFilters(prev => prev.filter(filter => filter.id !== id));
  };

  // Handle symbol/multiplier/timespan changes from header
  const handleSymbolChange = useCallback((newSymbol: string) => {
    setSymbol(newSymbol);
  }, []);

  const handleMultiplierChange = useCallback((newMultiplier: number) => {
    setMultiplier(newMultiplier);
  }, []);

  const handleTimespanChange = useCallback((newTimespan: ChartFilterTimespan) => {
    setTimespan(newTimespan);
  }, []);

  // Auto-scan function: scans with filters and updates chart data with highlights
  const performScan = useCallback(async () => {
    const activeFilters = filters.filter(filter => filter.enabled).map(filter => filter.expression);
    
    // If no active filters, just load chart data without scanning
    if (activeFilters.length === 0) {
      await fetchChartDataWithIndicators();
      return;
    }

    // Validate inputs
    if (!symbol.trim() || !dateRange.from || !dateRange.to) {
      return; // Silently skip if inputs are invalid
    }

    try {
      setIsScanning(true);
      setIsLoadingData(true);

      // Perform filter scan
      const response = await toolsApi.filterChartMatches({
        ticker: symbol.trim().toUpperCase(),
        multiplier,
        timespan,
        from: dateRange.from,
        to: dateRange.to,
        filters: activeFilters,
      });

      // Update chart data and matches
      const scannedData = response.results || [];
      setChartData(scannedData);
      setMatches(parseTimestamps(response.matchingTimestamps || []));

      // Fetch indicators if configured (preserves scanned data for accurate highlights)
      if (scannedData.length > 0) {
        await fetchIndicatorsForScan();
      } else {
        setApiIndicators([]);
      }
    } catch (error: any) {
      console.error('Filter scan failed', error);
      toast({
        title: 'Scan failed',
        description: error?.message || 'Unable to fetch matches. Please try again.',
        variant: 'destructive',
      });
    } finally {
      setIsScanning(false);
      setIsLoadingData(false);
    }
  }, [filters, symbol, multiplier, timespan, dateRange, fetchChartDataWithIndicators, parseTimestamps, fetchIndicatorsForScan]);

  // Manual scan handler (for button click)
  const handleRunScan = async () => {
    await performScan();
  };

  // Load initial chart data on mount
  useEffect(() => {
    if (!hasInitialLoadRef.current) {
      hasInitialLoadRef.current = true;
      // Load initial chart data
      fetchChartDataWithIndicators();
      // Enable auto-scanning after initial load completes
      setTimeout(() => {
        shouldAutoScanRef.current = true;
      }, 100);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []); // Only run once on mount

  // Auto-scan when filters or parameters change (with debouncing)
  useEffect(() => {
    // Skip until initial load is done and auto-scan is enabled
    if (!shouldAutoScanRef.current) {
      return;
    }

    const timeoutId = setTimeout(() => {
      performScan();
    }, 500); // 500ms debounce

    return () => clearTimeout(timeoutId);
  }, [filters, symbol, multiplier, timespan, dateRange, performScan]);

  return (
    <div className="min-h-screen bg-background text-foreground">
      <div className="max-w-full mx-auto px-4 py-4 md:px-6 md:py-6 space-y-5">
        <div className="space-y-1">
          <h1 className="text-2xl font-semibold text-primary">Chart Filters</h1>
          <p className="text-sm text-muted-foreground">
            Configure filter expressions and preview highlight markers. Integration with the backend scan API will fetch
            matching timestamps for the selected symbol.
          </p>
        </div>

        <div className="grid gap-6 lg:grid-cols-[.5fr_1fr]">
          {/* Filter Builder - Left Side */}
          <Card className="p-4 bg-card border border-border space-y-4">
            <div className="flex items-center justify-between">
              <h2 className="text-xs font-mono uppercase tracking-wider text-primary">Filter Builder</h2>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setFilters([])}
                disabled={filters.length === 0}
                className="bg-background dark:bg-gray-900 border-border dark:border-gray-700 text-muted-foreground hover:border-red-500 dark:hover:border-red-700 hover:text-red-600 dark:hover:text-red-400 font-mono text-xs uppercase px-3 py-1 transition-all disabled:opacity-50"
              >
                Clear All
              </Button>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <label className="text-[10px] font-mono uppercase text-muted-foreground">From</label>
                <Input
                  type="date"
                  value={dateRange.from}
                  onChange={event => setDateRange(prev => ({ ...prev, from: event.target.value }))}
                  className="bg-background border-border text-foreground dark:text-cyan-300 font-mono text-xs h-8"
                />
              </div>
              <div className="space-y-1">
                <label className="text-[10px] font-mono uppercase text-muted-foreground">To</label>
                <Input
                  type="date"
                  value={dateRange.to}
                  onChange={event => setDateRange(prev => ({ ...prev, to: event.target.value }))}
                  className="bg-background border-border text-foreground dark:text-cyan-300 font-mono text-xs h-8"
                />
              </div>
            </div>
            <FilterComposer onAddFilter={handleAddFilter} />
            <FilterList filters={filters} onToggle={handleToggleFilter} onRemove={handleRemoveFilter} />
            <div className="flex items-center gap-2">
              <Button
                onClick={handleRunScan}
                disabled={isScanning || isLoadingData}
                className="bg-primary/10 dark:bg-cyan-950 border-primary dark:border-cyan-700 text-primary dark:text-cyan-300 hover:bg-primary/20 dark:hover:bg-cyan-900 hover:border-primary dark:hover:border-cyan-500 font-mono text-xs uppercase px-3 py-2 transition-all disabled:opacity-50"
                title="Scan runs automatically. Click to refresh manually."
              >
                {isScanning || isLoadingData ? 'Scanning...' : 'Refresh Scan'}
              </Button>
            </div>
          </Card>

          {/* Chart Preview - Right Side */}
          <Card className="p-4 bg-card border border-border space-y-3 flex flex-col">
            <h2 className="text-xs font-mono uppercase tracking-wider text-primary">Chart Preview</h2>
            <div ref={chartWrapperRef} className="flex-1 border border-border rounded bg-background dark:bg-gray-950/80 min-h-[400px] flex flex-col">
              <div ref={headerRef} className="flex-shrink-0">
                <ChartHeader
                  symbol={symbol}
                  timeframe="1D"
                  multiplier={multiplier}
                  timespan={timespan}
                  onSymbolChange={handleSymbolChange}
                  onTimeframeChange={() => {}}
                  onMultiplierChange={handleMultiplierChange}
                  onTimespanChange={handleTimespanChange}
                  indicators={indicators}
                  onIndicatorsChange={setIndicators}
                />
              </div>
              <div className="flex-1 min-h-0" style={{ height: chartDimensions.height > 0 ? `${chartDimensions.height}px` : 'auto' }}>
                {chartDimensions.width > 0 && chartData.length > 0 ? (
                  <StockChart
                    data={chartData}
                    onDataUpdate={setChartData}
                    containerWidth={chartDimensions.width}
                    containerHeight={chartDimensions.height}
                    chartId="tools-chart-preview"
                    highlights={highlights}
                    indicators={apiIndicators}
                    configuredIndicators={indicators}
                  />
                ) : (
                  <div className="flex h-full min-h-[260px] items-center justify-center text-xs font-mono text-muted-foreground">
                    {isLoadingData ? 'Loading chart data…' : 'Chart data will load automatically. Use "Load & Scan" to scan with filters.'}
                  </div>
                )}
              </div>
            </div>
            {matches.length > 0 && (
              <div className="text-xs text-primary dark:text-cyan-300 font-mono">
                Matched timestamps:&nbsp;
                <span className="text-muted-foreground">
                  {matches
                    .slice(0, 5)
                    .map(ts => new Date(ts).toLocaleString())
                    .join(', ')}
                  {matches.length > 5 ? ` … (+${matches.length - 5} more)` : ''}
                </span>
              </div>
            )}
          </Card>
        </div>
      </div>
    </div>
  );
}


