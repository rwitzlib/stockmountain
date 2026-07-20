import { useEffect, useRef, useState, memo, useCallback } from 'react';
import { createChart, ColorType, IChartApi, ISeriesApi, Time, CandlestickSeries, LineSeries, HistogramSeries, LineStyle } from 'lightweight-charts';
import type { LogicalRange } from 'lightweight-charts';
import { AlertCircle } from 'lucide-react';
import type { BarData, IndicatorConfig, IndicatorSetup } from '../../../types/tools';
import { SessionHighlighting } from '../../../plugins/session-highlighting/session-highlighting';

export type ChartHighlight = {
  time: number | string;
  color?: string;
  label?: string;
};

interface StockChartProps {
  data: BarData[];
  onDataUpdate: (data: BarData[]) => void;
  containerWidth?: number;
  containerHeight?: number;
  chartId?: string;
  indicators?: IndicatorConfig[];
  configuredIndicators?: IndicatorSetup[];
  highlights?: ChartHighlight[];
}

export const StockChart = memo(function StockChart({
  data,
  onDataUpdate,
  containerWidth = 0,
  containerHeight = 0,
  chartId = 'unknown',
  indicators = [],
  configuredIndicators = [],
  highlights = [],
}: StockChartProps) {
  const chartContainerRef = useRef<HTMLDivElement>(null);
  const chartRef = useRef<IChartApi | null>(null);
  const seriesRef = useRef<ISeriesApi<'Candlestick'> | null>(null);
  const indicatorSeriesRef = useRef<Map<string, ISeriesApi<'Line'> | { macd?: ISeriesApi<'Line'>, signal?: ISeriesApi<'Line'>, histogram?: ISeriesApi<'Histogram'>, rsi?: ISeriesApi<'Line'>, upper?: ISeriesApi<'Line'>, lower?: ISeriesApi<'Line'> }>>(new Map());
  const highlightingPrimitivesRef = useRef<Map<ISeriesApi<any>, SessionHighlighting>>(new Map());
  const [error, setError] = useState<string | null>(null);
  const [isChartCreated, setIsChartCreated] = useState(false);
  const prevDimensionsRef = useRef({ width: 0, height: 0 });
  const hasUserInteractedRef = useRef(false);
  const lastVisibleLogicalRangeRef = useRef<LogicalRange | null>(null);
  const hasInitialFitRef = useRef(false);
  const [isDarkMode, setIsDarkMode] = useState(() => 
    document.documentElement.classList.contains('dark')
  );

  const DARK_THEME = {
    background: 'transparent',
    textColor: '#8b93a1',
    gridColor: 'rgba(148,163,184,0.08)',
    crosshairColor: 'rgba(148,163,184,0.35)',
    up: '#2fae60',
    down: '#e05252',
  };

  const LIGHT_THEME = {
    background: '#ffffff',
    textColor: '#64748b',
    gridColor: 'rgba(15,23,42,0.06)',
    crosshairColor: 'rgba(15,23,42,0.3)',
    up: '#2fae60',
    down: '#e05252',
  };

  // Detect theme changes
  useEffect(() => {
    const checkTheme = () => {
      setIsDarkMode(document.documentElement.classList.contains('dark'));
    };

    // Check theme on mount
    checkTheme();

    // Watch for theme changes
    const observer = new MutationObserver(checkTheme);
    observer.observe(document.documentElement, {
      attributes: true,
      attributeFilter: ['class'],
    });

    return () => observer.disconnect();
  }, []);

  const currentTheme = isDarkMode ? DARK_THEME : LIGHT_THEME;

  function applyThemeOptions() {
    if (!chartRef.current || !seriesRef.current) return;
    chartRef.current.applyOptions({
      layout: {
        background: { type: ColorType.Solid, color: currentTheme.background },
        textColor: currentTheme.textColor,
      },
      grid: {
        vertLines: { color: currentTheme.gridColor },
        horzLines: { color: currentTheme.gridColor },
      },
      crosshair: {
        vertLine: { color: currentTheme.crosshairColor },
        horzLine: { color: currentTheme.crosshairColor },
        mode: 0,
      },
    });

    seriesRef.current.applyOptions({
      upColor: currentTheme.up,
      downColor: currentTheme.down,
      borderVisible: false,
      wickUpColor: currentTheme.up,
      wickDownColor: currentTheme.down,
    });
  }

  function getIndicatorColor(index: number): string {
    const colors = [
      '#14a3bd', // teal
      '#8b5cf6', // violet
      '#d97706', // amber
      '#e05252', // rose
      '#2fae60', // emerald
      '#6366f1', // slate blue
      '#b45309', // dark amber
      '#64748b', // slate
    ];
    return colors[index % colors.length];
  }

  function updateIndicatorSeries(indicators: IndicatorConfig[], configuredIndicators: IndicatorSetup[]) {
    if (!chartRef.current) return;

    // Helper to create the API indicator name from setup, matching Container's format
    const nameFromSetup = (setup: IndicatorSetup): string => {
      const vals = Object.values(setup.params);
      switch (setup.type) {
        case 'sma':
          return `sma(${vals.join(',')})`;
        case 'ema':
          return `ema(${vals.join(',')})`;
        case 'macd':
          return `macd(${vals.join(',')})`;
        case 'rsi':
          return `rsi(${vals.join(',')})`;
        default:
          return '';
      }
    };

    // Map of configured indicator name -> setup (per-instance)
    const setupByName = new Map<string, IndicatorSetup>();
    configuredIndicators.forEach((cfg) => {
      const name = nameFromSetup(cfg);
      if (name) setupByName.set(name, cfg);
    });

    // Colors and panes per-instance (fallback to palette by index if unset)
    const colorByName = new Map<string, string>();
    const paneByName = new Map<string, number>();
    indicators.forEach((ind, idx) => {
      const cfg = setupByName.get(ind.name);
      const color = cfg?.color || getIndicatorColor(idx);
      const pane = Number(cfg?.pane ?? 0);
      colorByName.set(ind.name, color);
      paneByName.set(ind.name, pane);
    });

    // Remove series that are no longer in the indicators list
    const currentNames = new Set(indicators.map(ind => ind.name));
    for (const [name, series] of indicatorSeriesRef.current) {
      if (!currentNames.has(name)) {
        if (series && typeof (series as any).setData === 'function') {
          chartRef.current.removeSeries(series as ISeriesApi<'Line'>);
        } else {
          const group = series as any;
          Object.values(group).forEach((s: any) => s && chartRef.current?.removeSeries(s));
        }
        indicatorSeriesRef.current.delete(name);
      }
    }

    // Function to extract indicator type from name like "sma(20)" -> "sma"
    const extractIndicatorType = (name: string): string => {
      const match = name.match(/^([a-z]+)\(/);
      return match ? match[1].toLowerCase() : name.toLowerCase();
    };

    // Add or update indicator series
    indicators.forEach((indicator, index) => {
      const indicatorType = extractIndicatorType(indicator.name);
      const color = colorByName.get(indicator.name) || getIndicatorColor(index);
      const desiredPane = paneByName.get(indicator.name) ?? 0;

      // Multi-line indicators: MACD and RSI
      if (indicatorType === 'macd' || indicatorType === 'rsi') {
        const existing = indicatorSeriesRef.current.get(indicator.name) as any;
        const group = existing || {};

        const getConfiguredColor = (key: string, fallback: string) => {
          const cfg = setupByName.get(indicator.name);
          return (cfg?.colors && (cfg.colors as any)[key]) || fallback;
        };

        const macdColors = {
          macd: getConfiguredColor('macd', color),
          signal: getConfiguredColor('signal', '#e05252'),
          histogram: getConfiguredColor('histogram', '#64748b'),
          histogramUp: getConfiguredColor('histogramUp', '#2fae60'),
          histogramDown: getConfiguredColor('histogramDown', '#e05252'),
        };
        const rsiColors = {
          rsi: getConfiguredColor('rsi', color),
          upper: getConfiguredColor('upper', '#d97706'),
          lower: getConfiguredColor('lower', '#b45309'),
        };

        const ensureLine = (key: string, c: string, dashed = false) => {
          if (!group[key]) {
            const s = chartRef.current!.addSeries(LineSeries, {
              color: c,
              lineWidth: 2,
              lineStyle: dashed ? LineStyle.Dashed : LineStyle.Solid,
              lastValueVisible: false,
              priceLineVisible: false,
            });
            try { (s as any).moveToPane?.(desiredPane); } catch {}
            group[key] = s;
          } else {
            group[key].applyOptions({
              color: c,
              lineWidth: 2,
              lineStyle: dashed ? LineStyle.Dashed : LineStyle.Solid,
              lastValueVisible: false,
              priceLineVisible: false,
            });
            try { (group[key] as any).moveToPane?.(desiredPane); } catch {}
          }
        };

        const ensureHistogram = (key: string, c: string) => {
          if (!group[key]) {
            const s = chartRef.current!.addSeries(HistogramSeries, {
              color: c,
              priceFormat: { type: 'price' },
              priceLineVisible: false,
            });
            try { (s as any).moveToPane?.(desiredPane); } catch {}
            group[key] = s;
          } else {
            group[key].applyOptions({
              color: c,
              priceLineVisible: false,
            });
            try { (group[key] as any).moveToPane?.(desiredPane); } catch {}
          }
        };

        if (indicatorType === 'macd') {
          ensureLine('macd', macdColors.macd);
          ensureLine('signal', macdColors.signal);
          ensureHistogram('histogram', macdColors.histogram);
        } else {
          ensureLine('rsi', rsiColors.rsi);
          ensureLine('upper', rsiColors.upper, true);
          ensureLine('lower', rsiColors.lower, true);
        }

        const tzOffset = new Date().getTimezoneOffset() * 60 * 1000;
        const toPoint = (v?: number, t?: number, extra?: any) => ({ time: ((t ?? 0) - tzOffset) / 1000 as Time, value: v as number, ...extra });

        if (indicatorType === 'macd') {
          const macdData = indicator.results.map(p => toPoint(p.value, p.timestamp));
          const signalData = indicator.results.map(p => toPoint((p as any).signal, p.timestamp));
          const histData = indicator.results.map(p => {
            const val = (p as any).histogram as number | undefined;
            const isUp = typeof val === 'number' ? val >= 0 : false;
            return toPoint(val, p.timestamp, { color: isUp ? macdColors.histogramUp : macdColors.histogramDown });
          });
          group.histogram.setData(histData.filter(d => Number.isFinite(d.value)) as any);
          group.macd.setData(macdData.filter(d => Number.isFinite(d.value)) as any);
          group.signal.setData(signalData.filter(d => Number.isFinite(d.value)) as any);
        } else {
          const rsiData = indicator.results.map(p => toPoint(p.value, p.timestamp));
          const upperData = indicator.results.map(p => toPoint((p as any).upper, p.timestamp));
          const lowerData = indicator.results.map(p => toPoint((p as any).lower, p.timestamp));
          group.rsi.setData(rsiData.filter(d => Number.isFinite(d.value)) as any);
          group.upper.setData(upperData.filter(d => Number.isFinite(d.value)) as any);
          group.lower.setData(lowerData.filter(d => Number.isFinite(d.value)) as any);
        }

        indicatorSeriesRef.current.set(indicator.name, group);
      } else {
        // Single-line indicators
        if (!indicatorSeriesRef.current.has(indicator.name)) {
          const lineSeries = chartRef.current!.addSeries(LineSeries, {
            color: color,
            lineWidth: 2,
            lastValueVisible: false,
            priceLineVisible: false,
          });
          try { (lineSeries as any).moveToPane?.(desiredPane); } catch {}
          indicatorSeriesRef.current.set(indicator.name, lineSeries);
        } else {
          const existingSeries = indicatorSeriesRef.current.get(indicator.name) as ISeriesApi<'Line'>;
          if (existingSeries) {
            existingSeries.applyOptions({
              color: color,
              lineWidth: 2,
              lastValueVisible: false,
              priceLineVisible: false,
            });
            try { (existingSeries as any).moveToPane?.(desiredPane); } catch {}
          }
        }

        const series = indicatorSeriesRef.current.get(indicator.name) as ISeriesApi<'Line'>;
        if (series && indicator.results.length > 0) {
          const tzOffset = new Date().getTimezoneOffset() * 60 * 1000;
          const seriesData = indicator.results.map(point => ({
            time: (point.timestamp - tzOffset) / 1000 as Time,
            value: point.value,
          }));
          series.setData(seriesData);
        }
      }
    });
  }

  // Initialize chart when container dimensions are available
  useEffect(() => {
    // Log dimension changes with chart ID
    console.log(`[${chartId}] Dimensions: ${containerWidth}x${containerHeight}`);
    
    // Skip if dimensions haven't changed significantly
    if (
      chartRef.current &&
      prevDimensionsRef.current.width > 0 &&
      Math.abs(prevDimensionsRef.current.width - containerWidth) < 5 &&
      Math.abs(prevDimensionsRef.current.height - containerHeight) < 5
    ) {
      return;
    }
    
    // Wait for valid dimensions
    if (!chartContainerRef.current || containerWidth <= 10 || containerHeight <= 10) {
      console.log(`[${chartId}] Invalid dimensions, not updating chart`);
      return;
    }

    // Update the previous dimensions
    prevDimensionsRef.current = { width: containerWidth, height: containerHeight };
    
    // Create chart if it doesn't exist
    if (!chartRef.current) {
      try {
        console.log(`[${chartId}] Creating chart: ${containerWidth}x${containerHeight}`);
        const container = chartContainerRef.current;
        
        // Create chart instance
        const chart = createChart(container, {
          layout: {
            background: { type: ColorType.Solid, color: currentTheme.background },
            textColor: currentTheme.textColor,
          },
          crosshair: {
            mode: 0,
            vertLine: { color: currentTheme.crosshairColor },
            horzLine: { color: currentTheme.crosshairColor },
          },
          width: containerWidth,
          height: containerHeight,
          timeScale: {
            timeVisible: true,
            secondsVisible: false,
            lockVisibleTimeRangeOnResize: true,
          },
          grid: {
            vertLines: { color: currentTheme.gridColor },
            horzLines: { color: currentTheme.gridColor },
          }
        });

        // Add candlestick series
        const candlestickSeries = chart.addSeries(CandlestickSeries, {
          upColor: currentTheme.up,
          downColor: currentTheme.down,
          borderVisible: false,
          wickUpColor: currentTheme.up,
          wickDownColor: currentTheme.down,
        });

        // Subscribe to visible range changes to detect user interaction
        const timeScale = chart.timeScale();
        timeScale.subscribeVisibleTimeRangeChange(() => {
          hasUserInteractedRef.current = true;
          const logical = timeScale.getVisibleLogicalRange();
          if (logical) {
            lastVisibleLogicalRangeRef.current = logical as LogicalRange;
          }
        });

        // Store references
        chartRef.current = chart;
        seriesRef.current = candlestickSeries;
        setIsChartCreated(true);
        console.log('Chart created successfully');
      } catch (err) {
        console.error(`[${chartId}] Error initializing chart:`, err);
        setError('Failed to initialize chart');
      }
    } else {
      // Update dimensions of existing chart
      console.log(`[${chartId}] Resizing chart: ${containerWidth}x${containerHeight}`);
      const timeScale = chartRef.current.timeScale();
      const currentLogical = timeScale.getVisibleLogicalRange();
      chartRef.current.applyOptions({
        width: containerWidth,
        height: containerHeight,
      });
      if (currentLogical) {
        timeScale.setVisibleLogicalRange(currentLogical as LogicalRange);
      }
    }
  }, [containerWidth, containerHeight, chartId, currentTheme]);

  // Update chart theme when theme changes
  useEffect(() => {
    if (chartRef.current && seriesRef.current) {
      applyThemeOptions();
    }
  }, [isDarkMode]);

  // Update chart data when data changes or chart is created
  useEffect(() => {
    if (!seriesRef.current || !data?.length) return;
    
    try {
      const chartData = data.map(bar => {
        // Get local timezone offset in milliseconds
        const tzOffset = new Date().getTimezoneOffset() * 60 * 1000;
        // Convert UTC timestamp to local time by subtracting offset
        return {
          time: (bar.t - tzOffset) / 1000 as Time,
          open: Number(bar.o),
          high: Number(bar.h),
          low: Number(bar.l), 
          close: Number(bar.c),
        };
      });

      // Preserve current visible logical range if user interacted
      const timeScale = chartRef.current?.timeScale();
      const savedLogical = (hasUserInteractedRef.current && timeScale)
        ? (timeScale.getVisibleLogicalRange() as LogicalRange | null)
        : null;

      seriesRef.current.setData(chartData);

      if (timeScale) {
        if (savedLogical) {
          timeScale.setVisibleLogicalRange(savedLogical);
        } else if (!hasInitialFitRef.current) {
          // Initial fit only once if user hasn't interacted yet
          timeScale.fitContent();
          hasInitialFitRef.current = true;
        }
      }
    } catch (error) {
      console.error(`[${chartId}] Error updating chart data:`, error);
      setError('Error rendering chart data');
    }
  }, [data, isChartCreated, chartId]);

  // Update indicator series when indicators change
  // This must run BEFORE the highlighting effect to ensure series are available
  useEffect(() => {
    if (isChartCreated && indicators) {
      console.log(`[${chartId}] Updating indicator series:`, indicators.length, 'indicators');
      updateIndicatorSeries(indicators, configuredIndicators);
    }
  }, [indicators, configuredIndicators, isChartCreated, chartId]);

  // Helper: Safely get pane index for a series
  const getPaneIndex = useCallback((series: ISeriesApi<any>, fallback: number = 0): number => {
    try {
      return series.getPane().paneIndex();
    } catch {
      return fallback;
    }
  }, []);

  // Helper: Get all series that should have highlighting (one per pane)
  const getAllSeriesForHighlighting = useCallback(() => {
    const allSeries: ISeriesApi<any>[] = [];
    const panesWithHighlighting = new Set<number>();
    
    // Add candlestick series (always on pane 0)
    if (seriesRef.current) {
      allSeries.push(seriesRef.current);
      panesWithHighlighting.add(getPaneIndex(seriesRef.current, 0));
    }
    
    // Add indicator series - only one per pane to avoid double highlighting
    indicatorSeriesRef.current.forEach((seriesOrGroup) => {
      if (seriesOrGroup && typeof (seriesOrGroup as any).setData === 'function') {
        // Single series indicator
        const series = seriesOrGroup as ISeriesApi<any>;
        const paneIndex = getPaneIndex(series);
        if (!panesWithHighlighting.has(paneIndex)) {
          allSeries.push(series);
          panesWithHighlighting.add(paneIndex);
        }
      } else {
        // Multi-series indicator (MACD, RSI) - only attach to main series
        // MACD: macd line, signal line, histogram (all same pane)
        // RSI: rsi line, upper band, lower band (all same pane)
        const group = seriesOrGroup as any;
        const mainSeries = group.macd || group.rsi;
        if (mainSeries && typeof mainSeries.setData === 'function') {
          const paneIndex = getPaneIndex(mainSeries);
          if (!panesWithHighlighting.has(paneIndex)) {
            allSeries.push(mainSeries);
            panesWithHighlighting.add(paneIndex);
          }
        }
      }
    });
    
    return allSeries;
  }, [getPaneIndex]);

  useEffect(() => {
    if (!isChartCreated || !data?.length) {
      return;
    }

    // Convert highlight timestamps to chart Time format
    const tzOffset = new Date().getTimezoneOffset() * 60 * 1000;
    const highlightTimes = new Set(
      highlights
        .map((highlight) => {
          const rawTime = typeof highlight.time === 'string' ? Date.parse(highlight.time) : highlight.time;
          if (typeof rawTime !== 'number' || Number.isNaN(rawTime)) {
            return null;
          }
          return ((rawTime - tzOffset) / 1000) as Time;
        })
        .filter((time): time is Time => time !== null)
    );

    const highlighter = (time: Time) => {
      if (highlightTimes.has(time)) {
        return 'rgba(160, 167, 76, 0.55)';
      }
      return isDarkMode ? 'rgba(0, 0, 0, 0)' : 'rgba(0, 0, 0, 0)';
    };

    // Get all series that should have highlighting
    const allSeries = getAllSeriesForHighlighting();
    
    // Remove old highlighting primitives
    highlightingPrimitivesRef.current.forEach((primitive, series) => {
      try {
        series.detachPrimitive(primitive);
      } catch (e) {
        // Series might have been removed, ignore errors
      }
    });
    highlightingPrimitivesRef.current.clear();

    // Attach highlighting to all series
    allSeries.forEach((series) => {
      const sessionHighlighting = new SessionHighlighting(highlighter);
      try {
        series.attachPrimitive(sessionHighlighting);
        highlightingPrimitivesRef.current.set(series, sessionHighlighting);
      } catch (e) {
        console.error('Failed to attach highlighting to series:', e);
      }
    });

    // Cleanup: detach when highlights change or component unmounts
    return () => {
      highlightingPrimitivesRef.current.forEach((primitive, series) => {
        try {
          series.detachPrimitive(primitive);
        } catch (e) {
          // Series might have been removed, ignore errors
        }
      });
      highlightingPrimitivesRef.current.clear();
    };
  }, [highlights, chartId, isChartCreated, data, isDarkMode, getAllSeriesForHighlighting, indicators]);

  // Cleanup chart on unmount
  useEffect(() => {
    return () => {
      if (chartRef.current) {
        console.log(`[${chartId}] Cleaning up chart`);
        chartRef.current.remove();
        chartRef.current = null;
        seriesRef.current = null;
        indicatorSeriesRef.current.clear();
        setIsChartCreated(false);
      }
    };
  }, [chartId]);

  if (error) {
    return (
      <div className="flex items-center justify-center h-full">
        <div className="flex items-center gap-2 text-red-600">
          <AlertCircle className="w-5 h-5" />
          <span>{error}</span>
        </div>
      </div>
    );
  }

  return (
    <div 
      ref={chartContainerRef} 
      className="w-full h-full"
      style={{ position: 'relative' }}
    />
  );
});
