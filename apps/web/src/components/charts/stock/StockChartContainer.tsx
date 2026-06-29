import { useState, useEffect, useRef, useCallback } from 'react';
import { AlertCircle } from 'lucide-react';
import { ChartHeader } from './ChartHeader';
import { StockChart } from './StockChart';
import { fetchMarketData } from '../../../services/polygon';
import { IndicatorConfig, IndicatorSetup } from '../../../types/tools';

interface StockChartContainerProps {
  chartId: string;
  initialSymbol?: string;
  initialMultiplier?: number;
  initialTimespan?: 'minute' | 'hour' | 'day' | 'week' | 'year';
  onSymbolChange?: (symbol: string) => void;
  onMultiplierChange?: (multiplier: number) => void;
  onTimespanChange?: (timespan: 'minute' | 'hour' | 'day' | 'week' | 'year') => void;
}

export function StockChartContainer({ 
  chartId,
  initialSymbol = 'SPY',
  initialMultiplier = 1,
  initialTimespan = 'minute',
  onSymbolChange,
  onMultiplierChange,
  onTimespanChange
}: StockChartContainerProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const chartContainerRef = useRef<HTMLDivElement>(null);
  const headerRef = useRef<HTMLDivElement>(null);
  const [dimensions, setDimensions] = useState({ width: 0, height: 0 });
  const [symbol, setSymbol] = useState(initialSymbol);
  const [timeframe, setTimeframe] = useState('1D');
  const [multiplier, setMultiplier] = useState(initialMultiplier);
  const [timespan, setTimespan] = useState<'minute' | 'hour' | 'day' | 'week' | 'year'>(initialTimespan);
  const [indicators, setIndicators] = useState<IndicatorSetup[]>(() => {
    try {
      const saved = localStorage.getItem(`chart:${chartId}:indicators`);
      return saved ? JSON.parse(saved) : [];
    } catch {
      return [];
    }
  });
  const [apiIndicators, setApiIndicators] = useState<IndicatorConfig[]>([]);
  const [chartData, setChartData] = useState<any[]>([]);
  const [error, setError] = useState<string | null>(null);

  // Update local state when initial props change (helps with restoration)
  useEffect(() => {
    if (initialSymbol) setSymbol(initialSymbol);
    if (initialMultiplier) setMultiplier(initialMultiplier);
    if (initialTimespan) setTimespan(initialTimespan);
  }, [initialSymbol, initialMultiplier, initialTimespan]);

  // Handle symbol changes with callback to parent
  const handleSymbolChange = useCallback((newSymbol: string) => {
    setSymbol(newSymbol);
    if (onSymbolChange) onSymbolChange(newSymbol);
  }, [onSymbolChange]);

  // Handle multiplier changes with callback to parent
  const handleMultiplierChange = useCallback((newMultiplier: number) => {
    setMultiplier(newMultiplier);
    if (onMultiplierChange) onMultiplierChange(newMultiplier);
  }, [onMultiplierChange]);

  // Handle timespan changes with callback to parent
  const handleTimespanChange = useCallback((newTimespan: 'minute' | 'hour' | 'day' | 'week' | 'year') => {
    setTimespan(newTimespan);
    if (onTimespanChange) onTimespanChange(newTimespan);
  }, [onTimespanChange]);

  // Get the grid item element
  const getGridItem = useCallback(() => {
    if (!containerRef.current) return null;
    
    // The grid item is the element with data-chart-id attribute
    const gridItem = containerRef.current.closest('[data-chart-id]');
    return gridItem as HTMLElement | null;
  }, []);

  // Function to measure and update dimensions based on grid item
  const measureDimensions = useCallback(() => {
    if (!headerRef.current) return;
    
    const gridItem = getGridItem();
    if (!gridItem) {
      console.error(`[${chartId}] Could not find grid item`);
      return;
    }
    
    const headerHeight = headerRef.current.clientHeight;
    const gridWidth = gridItem.clientWidth;
    const gridHeight = gridItem.clientHeight;
    
    // Calculate available height for the chart by subtracting header height
    const chartHeight = Math.max(gridHeight - headerHeight, 200);
    
    console.log(`[${chartId}] Grid item: ${gridWidth}x${gridHeight}, Header: ${headerHeight}, Chart: ${gridWidth}x${chartHeight}`);
    
    if (gridWidth > 0 && chartHeight > 0 &&
        (gridWidth !== dimensions.width || chartHeight !== dimensions.height)) {
      setDimensions({
        width: gridWidth,
        height: chartHeight
      });
    }
  }, [chartId, dimensions.width, dimensions.height, getGridItem]);

  // Setup resize observers and event listeners
  useEffect(() => {
    const gridItem = getGridItem();
    if (!gridItem) {
      console.error(`[${chartId}] Could not find grid item to observe`);
      return;
    }
    
    // Initial measurement
    setTimeout(measureDimensions, 100);
    
    // Create ResizeObserver to watch the grid item
    const resizeObserver = new ResizeObserver(() => {
      measureDimensions();
    });
    
    resizeObserver.observe(gridItem);
    
    // Listen for custom resize events
    const handleChartResized = (e: CustomEvent) => {
      if (e.detail.chartId === chartId) {
        console.log(`[${chartId}] Chart resize event received`);
        setTimeout(measureDimensions, 50);
      }
    };
    
    // Listen for window resize
    const handleWindowResize = () => {
      setTimeout(measureDimensions, 50);
    };
    
    window.addEventListener('chartResized', handleChartResized as EventListener);
    window.addEventListener('resize', handleWindowResize);
    
    return () => {
      resizeObserver.disconnect();
      window.removeEventListener('chartResized', handleChartResized as EventListener);
      window.removeEventListener('resize', handleWindowResize);
    };
  }, [chartId, measureDimensions, getGridItem]);

  // Convert indicators to API format
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

  // Fetch data when parameters change
  const fetchData = async (params: {
    symbol: string,
    multiplier: number,
    timespan: 'minute' | 'hour' | 'day' | 'week' | 'year',
    indicators?: IndicatorSetup[]
  }) => {
    try {
      const endDate = new Date().toISOString().split('T')[0];
      let days = 7;
      
      if (params.timespan === 'minute') {
        days = 5 * params.multiplier;
      } else if (params.timespan === 'hour') {
        days = 90 * params.multiplier;
      } else if (params.timespan === 'day') {
        days = 730;
      }

      const startDate = new Date(Date.now() - days * 24 * 60 * 60 * 1000)
        .toISOString()
        .split('T')[0];

      const indicatorsStrings = params.indicators ? convertIndicatorsToApiFormat(params.indicators) : [];
      console.log('Fetching data with params:', params, 'indicators:', indicatorsStrings);

      const data = await fetchMarketData({
        ticker: params.symbol,
        multiplier: params.multiplier,
        timespan: params.timespan,
        from: startDate,
        to: endDate,
        indicators: indicatorsStrings
      });


      console.log('Data received:', data.results.length, 'items');
      console.log('Indicators received:', data.indicators?.length || 0, 'indicators');
      setChartData(data.results);
      setApiIndicators(data.indicators || []);
      setError(null);
    } catch (err) {
      console.error('Error fetching data:', err);
      setError(err instanceof Error ? err.message : 'Failed to fetch data');
    }
  };

  // Initial data fetch and periodic updates
  useEffect(() => {
    // Fetch data immediately
    fetchData({ symbol, multiplier, timespan, indicators });

    // Set up interval to fetch data every 15 seconds
    const interval = setInterval(() => {
      fetchData({ symbol, multiplier, timespan, indicators });
    }, 5000); // 5 seconds

    // Cleanup interval on component unmount or when dependencies change
    return () => clearInterval(interval);
  }, [symbol, multiplier, timespan, indicators]);

  // Persist indicators to localStorage when they change
  useEffect(() => {
    try {
      localStorage.setItem(`chart:${chartId}:indicators`, JSON.stringify(indicators));
    } catch (e) {
      console.error('Failed to save indicators:', e);
    }
  }, [chartId, indicators]);

  // Reload indicators if chartId changes (e.g., navigating back to a different chart)
  useEffect(() => {
    try {
      const saved = localStorage.getItem(`chart:${chartId}:indicators`);
      if (saved) {
        setIndicators(JSON.parse(saved));
      }
    } catch (e) {
      console.error('Failed to load indicators:', e);
    }
  }, [chartId]);

  return (
    <div 
      ref={containerRef} 
      className="flex flex-col h-full w-full"
      style={{ position: 'absolute', top: 0, left: 0, right: 0, bottom: 0 }}
    >
      <div ref={headerRef} className="flex-shrink-0">
        <ChartHeader
          symbol={symbol}
          timeframe={timeframe}
          multiplier={multiplier}
          timespan={timespan}
          onSymbolChange={handleSymbolChange}
          onTimeframeChange={setTimeframe}
          onMultiplierChange={handleMultiplierChange}
          onTimespanChange={handleTimespanChange}
          onParamsChange={fetchData}
          indicators={indicators}
          onIndicatorsChange={setIndicators}
        />
      </div>
      <div 
        ref={chartContainerRef} 
        className="flex-1 min-h-0"
        style={{ height: dimensions.height > 0 ? `${dimensions.height}px` : 'auto' }}
        data-chart-container-id={chartId}
      >
        {error ? (
          <div className="flex items-center justify-center h-full pt-[15%]">
            <div className="flex flex-col items-center gap-3 text-center max-w-md mx-4">
              <AlertCircle className="w-8 h-8 text-red-600 dark:text-red-400" />
              <div className="font-mono text-sm">
                <div className="text-red-600 dark:text-red-400 mb-1 font-semibold tracking-wider">
                  :: ERROR ::
                </div>
                <div className="text-muted-foreground text-xs leading-relaxed">
                  {error}
                </div>
              </div>
            </div>
          </div>
        ) : (
          <StockChart
            data={chartData}
            onDataUpdate={setChartData}
            containerWidth={dimensions.width}
            containerHeight={dimensions.height}
            chartId={chartId}
            indicators={apiIndicators}
            configuredIndicators={indicators}
          />
        )}
      </div>
    </div>
  );
} 