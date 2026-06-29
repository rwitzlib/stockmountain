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
      const response = await fetch(`https://api.stockmountain.io/api/trade/${id}`, {
        headers: {
          'Authorization': `Bearer ${localStorage.getItem('accessToken')}`
        }
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
      
      const response = await fetch('https://api.stockmountain.io/api/stocks', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${localStorage.getItem('accessToken')}`,
        },
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
      background: '#0b1220',
      text: '#e5e7eb',
      grid: '#1f2937',
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
          background: '#0b1220',
          text: '#e5e7eb',
          grid: '#1f2937',
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
        <div className="text-primary dark:text-cyan-400 font-mono text-sm animate-pulse">» LOADING TRADE DATA...</div>
      </div>
    );
  }

  // Show error state if there was an error fetching the trade
  if (!tradeFromState && error) {
    return (
      <div className="min-h-screen bg-background p-4 md:p-8 pt-20 md:pt-8 flex items-center justify-center">
        <div className="text-center bg-red-100/50 dark:bg-red-950/20 border border-red-300 dark:border-red-900 p-6">
          <p className="text-red-700 dark:text-red-400 font-mono text-sm mb-4">[ ERROR LOADING TRADE ]</p>
          <p className="text-muted-foreground font-mono text-xs mb-4">{'>> '}Failed to retrieve trade data</p>
          <Button onClick={() => navigate('/optimus/trades')} className="bg-card border-border text-foreground hover:border-primary dark:hover:border-cyan-500 hover:text-primary dark:hover:text-cyan-400 font-mono text-xs uppercase">
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
              className="bg-card hover:bg-muted border-border hover:border-primary text-foreground hover:text-primary transition-all"
              onClick={() => navigate(-1)}
            >
              <ArrowLeft className="h-4 w-4 mr-1" />
              <span className="font-mono text-xs uppercase">Back</span>
            </Button>
            <div>
              <h1 className="text-xl font-mono font-bold uppercase tracking-wider text-foreground"># Trade Analysis - {trade.ticker}</h1>
              <div className="flex items-center gap-2 mt-2">
                <span className={`px-2 py-0.5 text-[9px] font-mono uppercase border ${
                  trade.orderStatus === 'Closed'
                    ? 'bg-purple-100 dark:bg-purple-950 text-purple-700 dark:text-purple-400 border-purple-300 dark:border-purple-800'
                    : 'bg-primary/10 dark:bg-cyan-950 text-primary dark:text-cyan-400 border-primary/30 dark:border-cyan-800'
                }`}>
                  {trade.orderStatus}
                </span>
                <p className="text-muted-foreground font-mono text-xs">
                  {formatDateDisplay(trade.openedAt)} {'→'} {formatDateDisplay(trade.closedAt) || 'PRESENT'}
                </p>
              </div>
            </div>
          </div>
        </div>

        {/* Trade Metrics */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          <div className="bg-card/50 p-3 border border-border hover:border-primary dark:hover:border-cyan-700 transition-colors">
            <h3 className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground mb-1">:: Shares</h3>
            <p className="text-lg font-mono font-bold text-primary dark:text-cyan-400">{trade.shares}</p>
          </div>
          <div className="bg-card/50 p-3 border border-border hover:border-green-600 dark:hover:border-green-700 transition-colors">
            <h3 className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground mb-1">:: Entry Price</h3>
            <p className="text-lg font-mono font-bold text-green-600 dark:text-green-400">{formatPrice(trade.entryPrice)}</p>
          </div>
          <div className="bg-card/50 p-3 border border-border hover:border-yellow-600 dark:hover:border-yellow-700 transition-colors">
            <h3 className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground mb-1">:: Exit Price</h3>
            <p className="text-lg font-mono font-bold text-yellow-600 dark:text-yellow-400">{formatPrice(trade.closePrice)}</p>
          </div>
          <div className="bg-card/50 p-3 border border-border hover:border-primary dark:hover:border-cyan-700 transition-colors">
            <h3 className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground mb-1">:: Profit/Loss</h3>
            <p className={`text-lg font-mono font-bold ${trade.profit >= 0 ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'}`}>
              {formatProfit(trade.profit)}
            </p>
          </div>
        </div>

        {/* Main Content Grid */}
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
          <div className="lg:col-span-2">
            <div className="bg-card/50 border border-border">
              <div className="p-4 border-b border-border">
                <h2 className="text-xs font-mono uppercase tracking-wider text-primary dark:text-cyan-400 mb-2"># Price Chart</h2>
                <p className="text-[10px] font-mono text-muted-foreground">
                  {'>> '}ENTRY: {formatPrice(trade.entryPrice)} | EXIT: {formatPrice(trade.closePrice)}
                </p>
              </div>
              <div className="p-3 border-b border-border">
                <TimeSelector onRangeChange={handleRangeChange} initialRange={timeRange} />
              </div>
              {isLoading ? (
                <div className="h-[50vh] flex items-center justify-center bg-muted/30 dark:bg-gray-950/50 border border-border">
                  <div className="text-center">
                    <p className="text-primary dark:text-cyan-400 font-mono text-sm animate-pulse">» LOADING CHART...</p>
                    <p className="text-[10px] font-mono text-muted-foreground mt-2">{'>> '}Retrieving market data</p>
                  </div>
                </div>
              ) : (
                <div
                  ref={chartContainerRef}
                  className="chart-container h-[50vh] w-full bg-background dark:bg-[#0a0e17] border border-border"
                />
              )}
            </div>
          </div>

          <div className="lg:col-span-1">
            <div className="bg-card/50 border border-border h-fit">
              <div className="p-4 border-b border-border">
                <h3 className="text-xs font-mono uppercase text-muted-foreground"># Trade Details</h3>
              </div>
              <div className="p-4 space-y-3">
                <div className="space-y-1">
                  <div className="text-[10px] font-mono uppercase text-muted-foreground">TICKER:</div>
                  <div className="text-lg font-mono font-bold text-foreground">{trade.ticker}</div>
                </div>
                <div className="space-y-1">
                  <div className="text-[10px] font-mono uppercase text-muted-foreground">ENTRY TIME:</div>
                  <div className="text-xs font-mono text-primary dark:text-cyan-400">{formatDateDisplay(trade.openedAt)}</div>
                </div>
                <div className="space-y-1">
                  <div className="text-[10px] font-mono uppercase text-muted-foreground">EXIT TIME:</div>
                  <div className="text-xs font-mono text-primary dark:text-cyan-400">{formatDateDisplay(trade.closedAt) || 'STILL OPEN'}</div>
                </div>
                {trade.type && (
                  <div className="space-y-1">
                    <div className="text-[10px] font-mono uppercase text-muted-foreground">TRADE TYPE:</div>
                    <div className={`px-2 py-1 text-[9px] font-mono uppercase border ${
                      trade.type === 'Live'
                        ? 'bg-green-100 dark:bg-green-950 text-green-700 dark:text-green-400 border-green-300 dark:border-green-800'
                        : 'bg-yellow-100 dark:bg-yellow-950 text-yellow-700 dark:text-yellow-400 border-yellow-300 dark:border-yellow-800'
                    }`}>
                      {trade.type}
                    </div>
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>

        <div className="bg-card/50 border border-border">
          <div className="p-4 border-b border-border">
            <h2 className="text-xs font-mono uppercase tracking-wider text-muted-foreground"># Trade Notes</h2>
          </div>
          <div className="p-4">
            {(trade as TradeDetail).notes ? (
              <div className="font-mono text-sm text-foreground leading-relaxed">
                {(trade as TradeDetail).notes}
              </div>
            ) : (
              <div className="text-center py-6">
                <p className="text-muted-foreground font-mono text-xs">[ NO NOTES RECORDED ]</p>
                <p className="text-[10px] font-mono text-muted-foreground/60 mt-1">{'>> '}Trade executed without additional context</p>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

export default TradeDetailPage;