import { getAuthHeaders } from '../../api/authToken';
import { useRef, useEffect, useState } from 'react';
import { useParams, useNavigate, useLocation } from 'react-router-dom';
import { TimeSelector } from '../../components/TimeSelector';
import { formatPrice, formatProfit } from '../../utils/chartUtils';
import { Button } from '../../components/ui/button';
import { ArrowLeft } from 'lucide-react';
import { createChart, Time, CandlestickSeries, createSeriesMarkers, ColorType, type IChartApi, type ISeriesApi } from 'lightweight-charts';
import type { LogicalRange } from 'lightweight-charts';
import { Trade } from '../../types/trade';
import { StockMarketData, BarData } from '../../types/tools';
import { useQuery } from '@tanstack/react-query';

interface TradeDetail extends Trade {
  notes?: string;
}

interface LocationState {
  trade: Trade;
}

const TradeDetailPage = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const chartContainerRef = useRef<HTMLDivElement>(null);
  const chartRef = useRef<IChartApi | null>(null);
  const seriesRef = useRef<ISeriesApi<'Candlestick'> | null>(null);
  const hasUserInteractedRef = useRef(false);
  const hasInitialFitRef = useRef(false);
  const [stockData, setStockData] = useState<StockMarketData | null>(null);
  const [timeRange, setTimeRange] = useState('1d');
  const [isLoading, setIsLoading] = useState(false);
  const [isDarkMode, setIsDarkMode] = useState(() => 
    document.documentElement.classList.contains('dark')
  );
  
  // Get trade from location state
  const tradeFromState = (location.state as LocationState)?.trade;
  
  // If trade is not available in state, fetch it
  const { 
    data: fetchedTrade,
    isLoading: isTradeLoading, 
    error 
  } = useQuery({
    queryKey: ['trade', id],
    queryFn: async () => {
      const response = await fetch(`https://stockmountain.io/api/trade/${id}`, {
        headers: await getAuthHeaders()
      });

      if (!response.ok) {
        throw new Error('Network response was not ok');
      }
      return await response.json();
    },
    // Only fetch if we don't have the trade data in state
    enabled: !tradeFromState
  });

  // Use the state trade if available, otherwise use the fetched trade
  const trade = tradeFromState || fetchedTrade;
  
  // Format dates for API and display
  const formatDateISO = (dateString: string | null) => {
    if (!dateString || dateString === 'null') return '';
    return new Date(dateString).toISOString().split('T')[0];
  };
  
  const formatDateDisplay = (dateString: string | null) => {
    if (!dateString || dateString === 'null') return '';
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  };

  // Fetch stock data from API
  const fetchStockData = async () => {
    if (!trade || !trade.ticker) return;
    
    setIsLoading(true);
    
    try {
      // Determine date range based on selected timeRange and trade dates
      let startDate = formatDateISO(trade.openedAt);
      let endDate = formatDateISO(trade.closedAt) || new Date().toISOString().split('T')[0];
      
      // Adjust based on timeRange if needed
      if (timeRange === '1d') {
        // Use just day of trade
      } else if (timeRange === '1w') {
        // Expand to a week
        const start = new Date(startDate);
        start.setDate(start.getDate() - 7);
        startDate = start.toISOString().split('T')[0];
      } else if (timeRange === '1m') {
        // Expand to a month
        const start = new Date(startDate);
        start.setMonth(start.getMonth() - 1);
        startDate = start.toISOString().split('T')[0];
      }
      
      const response = await fetch('https://stockmountain.io/api/stocks', {
        method: 'POST',
        headers: await getAuthHeaders(),
        body: JSON.stringify({
          ticker: trade.ticker,
          multiplier: 1,
          timespan: "minute",
          from: startDate,
          to: endDate
        })
      });
      const data: StockMarketData = await response.json();
      setStockData(data);
    } catch (error) {
      console.error('Error fetching stock data:', error);
    } finally {
      setIsLoading(false);
    }
  };

  const handleRangeChange = (range: string) => {
    setTimeRange(range);
  };

  // Detect theme changes
  useEffect(() => {
    const observer = new MutationObserver(() => {
      setIsDarkMode(document.documentElement.classList.contains('dark'));
    });
    observer.observe(document.documentElement, {
      attributes: true,
      attributeFilter: ['class'],
    });
    return () => observer.disconnect();
  }, []);

  // Initialize and update chart when data changes
  useEffect(() => {
    if (trade?.ticker) {
      fetchStockData();
    }
  }, [trade?.ticker, timeRange]);

  useEffect(() => {
    if (!chartContainerRef.current || !stockData || !stockData.results || stockData.results.length === 0) return;
    
    const initialIsDark = document.documentElement.classList.contains('dark');
    const initialColors = initialIsDark ? {
      background: 'transparent',
      text: '#8b93a1',
      grid: 'rgba(148,163,184,0.08)',
      crosshair: '#6b7280',
    } : {
      background: '#ffffff',
      text: '#1f2937',
      grid: '#e5e7eb',
      crosshair: '#9ca3af',
    };
    
    const chart = createChart(chartContainerRef.current, {
      width: chartContainerRef.current.clientWidth,
      height: chartContainerRef.current.clientHeight,
      layout: {
        background: { type: ColorType.Solid, color: initialColors.background },
        textColor: initialColors.text,
      },
      crosshair: { mode: 0, vertLine: { color: initialColors.crosshair }, horzLine: { color: initialColors.crosshair } },
      grid: { vertLines: { color: initialColors.grid }, horzLines: { color: initialColors.grid } },
      timeScale: { timeVisible: true, secondsVisible: false, lockVisibleTimeRangeOnResize: true },
    });
    chartRef.current = chart;
    const candleSeries = chart.addSeries(CandlestickSeries, {
      upColor: '#22c55e',
      downColor: '#ef5350',
      borderVisible: false,
      wickUpColor: '#22c55e',
      wickDownColor: '#ef5350',
    });
    seriesRef.current = candleSeries;
    
    // Track user zoom/pan
    const ts = chart.timeScale();
    ts.subscribeVisibleTimeRangeChange(() => { hasUserInteractedRef.current = true; });
    
    const tzOffset = new Date().getTimezoneOffset() * 60 * 1000;
    const formattedData = stockData.results.map((bar: BarData) => ({
      time: (bar.t - tzOffset) / 1000 as Time,
      open: bar.o,
      high: bar.h,
      low: bar.l,
      close: bar.c,
    }));
    seriesRef.current.setData(formattedData);
    
    let startIndex = 0;
    let endIndex = formattedData.length - 1;

    // Add markers for buy and sell points
    if (trade?.openedAt) {
      const buyDate = new Date(trade.openedAt);
      const buyTime = Math.floor((buyDate.getTime() - tzOffset) / 1000) - buyDate.getSeconds() as Time;
      
      candleSeries.createPriceLine({
        price: trade.entryPrice,
        color: '#2196F3',
        lineWidth: 2,
        lineStyle: 2,
        axisLabelVisible: true,
        title: 'Buy',
      });

      startIndex = formattedData.findIndex(bar => bar.time === buyTime) - 30;

      // Add buy marker
      createSeriesMarkers(
        candleSeries,
        [
          {
            color: '#2196F3',
            shape: 'arrowUp',
            position: 'belowBar',
            text: 'BUY',
            time: buyTime
          }
        ]
      );
    }
    
    if (trade?.closedAt && trade?.orderStatus === 'Closed') {
      const sellDate = new Date(trade.closedAt);
      const sellTime = Math.floor((sellDate.getTime() - tzOffset) / 1000) - sellDate.getSeconds() as Time;

      candleSeries.createPriceLine({
        price: trade.closePrice,
        color: '#FF5252',
        lineWidth: 2,
        lineStyle: 2,
        axisLabelVisible: true,
        title: 'Sell',
      });

      endIndex = formattedData.findIndex(bar => bar.time === sellTime) + 30;
      
      // Add sell marker
      createSeriesMarkers(
        candleSeries,
        [
          {
            color: '#FF5252',
            shape: 'arrowDown',
            position: 'aboveBar',
            text: 'SELL',
            time: sellTime
          }
        ]
      );
    }

    chart.timeScale().setVisibleLogicalRange({ from: startIndex, to: endIndex });
    if (!hasInitialFitRef.current) { chart.timeScale().fitContent(); hasInitialFitRef.current = true; }
    
    // Handle resize
    const handleResize = () => {
      if (!chartContainerRef.current || !chartRef.current) return;
      const logical = chartRef.current.timeScale().getVisibleLogicalRange();
      chartRef.current.applyOptions({
        width: chartContainerRef.current.clientWidth,
        height: chartContainerRef.current.clientHeight,
      });
      if (logical) chartRef.current.timeScale().setVisibleLogicalRange(logical as LogicalRange);
    };
    
    window.addEventListener('resize', handleResize);
    
    // Reapply theme when dark mode changes
    const themeObserver = new MutationObserver(() => {
      const currentIsDark = document.documentElement.classList.contains('dark');
      setIsDarkMode(currentIsDark);
      if (chartRef.current) {
        const colors = currentIsDark ? {
          background: 'transparent',
          text: '#8b93a1',
          grid: 'rgba(148,163,184,0.08)',
          crosshair: '#6b7280',
        } : {
          background: '#ffffff',
          text: '#1f2937',
          grid: '#e5e7eb',
          crosshair: '#9ca3af',
        };
        chartRef.current.applyOptions({
          layout: {
            background: { type: ColorType.Solid, color: colors.background },
            textColor: colors.text,
          },
          grid: {
            vertLines: { color: colors.grid },
            horzLines: { color: colors.grid },
          },
          crosshair: {
            mode: 0,
            vertLine: { color: colors.crosshair },
            horzLine: { color: colors.crosshair },
          },
        });
      }
    });
    themeObserver.observe(document.documentElement, {
      attributes: true,
      attributeFilter: ['class'],
    });
    
    return () => {
      themeObserver.disconnect();
      window.removeEventListener('resize', handleResize);
      if (chartRef.current) { chartRef.current.remove(); chartRef.current = null; }
      seriesRef.current = null;
    };
  }, [stockData, trade]);

  // Show loading state when no trade data is available yet
  if ((!tradeFromState && isTradeLoading) || !trade) {
    return (
      <div className="min-h-screen bg-background p-4 md:p-8 pt-20 md:pt-8 flex items-center justify-center">
        <div className="text-muted-foreground text-sm animate-pulse">Loading trade data…</div>
      </div>
    );
  }

  // Show error state if there was an error fetching the trade
  if (!tradeFromState && error) {
    return (
      <div className="min-h-screen bg-background p-4 md:p-8 pt-20 md:pt-8 flex items-center justify-center">
        <div className="text-center rounded-xl border border-red-300 dark:border-red-900 bg-red-100/50 dark:bg-red-950/20 p-6">
          <p className="text-red-700 dark:text-red-400 text-sm font-medium mb-4">Error loading trade</p>
          <p className="text-muted-foreground text-xs mb-4">Failed to retrieve trade data</p>
          <Button onClick={() => navigate('/optimus/trades')} variant="outline" className="text-xs hover:bg-accent hover:text-foreground">
            Return to Trades
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-background p-4 md:p-8 pt-20 md:pt-8">
      <div className="max-w-7xl mx-auto space-y-4">
        <div className="flex items-center justify-between border-b border-border pb-4">
          <div className="flex items-center gap-4">
            <Button
              variant="outline"
              size="sm"
              className="text-muted-foreground hover:bg-accent hover:text-foreground transition-colors"
              onClick={() => navigate(-1)}
            >
              <ArrowLeft className="h-4 w-4 mr-1" />
              <span className="text-xs">Back</span>
            </Button>
            <div>
              <h1 className="text-xl font-semibold tracking-tight text-foreground">Trade Analysis — {trade.ticker}</h1>
              <div className="flex items-center gap-2 mt-2">
                <span className={`rounded-full px-2.5 py-0.5 text-[11px] font-semibold ${
                  trade.orderStatus === 'Closed'
                    ? 'bg-purple-500/10 text-purple-600 dark:text-purple-400'
                    : 'bg-green-500/10 text-green-600 dark:text-green-400'
                }`}>
                  {trade.orderStatus}
                </span>
                <p className="text-muted-foreground text-xs">
                  {formatDateDisplay(trade.openedAt)} → {formatDateDisplay(trade.closedAt) || 'Present'}
                </p>
              </div>
            </div>
          </div>
        </div>

        {/* Trade Metrics */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          <div className="rounded-xl border border-border/80 bg-card p-3">
            <h3 className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground mb-1">Shares</h3>
            <p className="text-lg font-semibold tabular-nums text-foreground">{trade.shares}</p>
          </div>
          <div className="rounded-xl border border-border/80 bg-card p-3">
            <h3 className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground mb-1">Entry Price</h3>
            <p className="text-lg font-semibold tabular-nums text-foreground">{formatPrice(trade.entryPrice)}</p>
          </div>
          <div className="rounded-xl border border-border/80 bg-card p-3">
            <h3 className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground mb-1">Exit Price</h3>
            <p className="text-lg font-semibold tabular-nums text-foreground">{formatPrice(trade.closePrice)}</p>
          </div>
          <div className="rounded-xl border border-border/80 bg-card p-3">
            <h3 className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground mb-1">Profit/Loss</h3>
            <p className={`text-lg font-semibold tabular-nums ${trade.profit >= 0 ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'}`}>
              {formatProfit(trade.profit)}
            </p>
          </div>
        </div>

        {/* Main Content Grid */}
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
          <div className="lg:col-span-2">
            <div className="rounded-xl border border-border/80 bg-card">
              <div className="p-4 border-b border-border">
                <h2 className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground mb-2">Price Chart</h2>
                <p className="text-xs text-muted-foreground tabular-nums">
                  Entry {formatPrice(trade.entryPrice)} · Exit {formatPrice(trade.closePrice)}
                </p>
              </div>
              <div className="p-3 border-b border-border">
                <TimeSelector onRangeChange={handleRangeChange} initialRange={timeRange} />
              </div>
              {isLoading ? (
                <div className="h-[50vh] flex items-center justify-center bg-muted/30 rounded-b-xl">
                  <div className="text-center">
                    <p className="text-muted-foreground text-sm animate-pulse">Loading chart…</p>
                    <p className="text-xs text-muted-foreground mt-2">Retrieving market data</p>
                  </div>
                </div>
              ) : (
                <div
                  ref={chartContainerRef}
                  className="chart-container h-[50vh] w-full rounded-b-xl"
                />
              )}
            </div>
          </div>

          <div className="lg:col-span-1">
            <div className="rounded-xl border border-border/80 bg-card h-fit">
              <div className="p-4 border-b border-border">
                <h3 className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">Trade Details</h3>
              </div>
              <div className="p-4 space-y-3">
                <div className="space-y-1">
                  <div className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">Ticker</div>
                  <div className="text-lg font-mono font-bold text-foreground">{trade.ticker}</div>
                </div>
                <div className="space-y-1">
                  <div className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">Entry Time</div>
                  <div className="text-xs text-foreground">{formatDateDisplay(trade.openedAt)}</div>
                </div>
                <div className="space-y-1">
                  <div className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">Exit Time</div>
                  <div className="text-xs text-foreground">{formatDateDisplay(trade.closedAt) || 'Still open'}</div>
                </div>
                {trade.type && (
                  <div className="space-y-1">
                    <div className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">Trade Type</div>
                    <div className={`inline-block rounded-full px-2.5 py-0.5 text-[11px] font-semibold ${
                      trade.type === 'Live'
                        ? 'bg-green-500/10 text-green-600 dark:text-green-400'
                        : 'bg-yellow-500/10 text-yellow-600 dark:text-yellow-400'
                    }`}>
                      {trade.type}
                    </div>
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>

        <div className="rounded-xl border border-border/80 bg-card">
          <div className="p-4 border-b border-border">
            <h2 className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">Trade Notes</h2>
          </div>
          <div className="p-4">
            {(trade as TradeDetail).notes ? (
              <div className="text-sm text-foreground leading-relaxed">
                {(trade as TradeDetail).notes}
              </div>
            ) : (
              <div className="text-center py-6">
                <p className="text-muted-foreground text-xs">No notes recorded</p>
                <p className="text-xs text-muted-foreground/60 mt-1">Trade executed without additional context</p>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

export default TradeDetailPage;