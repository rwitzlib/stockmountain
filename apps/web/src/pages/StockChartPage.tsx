import { Responsive, WidthProvider, Layout } from 'react-grid-layout';
import { ContextMenu } from '../components/ui/ContextMenu';
import { WidgetMenu } from '../components/widgets/WidgetMenu';
import { Clock } from '../components/clock/Clock';
import { MarketStatus } from '../components/market';
import { ApiStatus } from '../components/status';
import { useChartGrid } from '../hooks/useChartGrid';
import { GRID_CONFIG } from '../config/gridConfig';
import { StockChartContainer } from '../components/charts/stock/StockChartContainer';
import { useState, useEffect, useRef, useCallback } from 'react';
import 'react-grid-layout/css/styles.css';
import 'react-resizable/css/styles.css';

const ResponsiveGridLayout = WidthProvider(Responsive);

export function StockChartPage() {
  const {
    charts,
    layouts,
    maxRows,
    handleLayoutChange,
    handleAddChart,
    handleDeleteChart,
    handleRemoveAllCharts,
    handleUpdateChart
  } = useChartGrid();

  // Store local versions of layouts to ensure they're respected during rendering
  const [localLayouts, setLocalLayouts] = useState<{ [key: string]: Layout[] }>(layouts || {});
  // Ref to track which chart was last resized
  const lastResizedChartRef = useRef<string | null>(null);

  // Update local layouts when the global layouts change
  useEffect(() => {
    if (layouts && Object.keys(layouts).length > 0) {
      setLocalLayouts(layouts);
    }
  }, [layouts]);

  // Custom layout change handler
  const onLayoutChange = (currentLayout: Layout[], allLayouts: { [key: string]: Layout[] }) => {
    // Update local layouts immediately
    setLocalLayouts(allLayouts);
    // Then update the global state
    handleLayoutChange(currentLayout, allLayouts);
  };

  // Handle resize start - track which chart is being resized
  const handleResizeStart = useCallback((_layout: Layout[], _oldItem: Layout, newItem: Layout) => {
    lastResizedChartRef.current = newItem.i;
  }, []);

  // Handle resize stop - force a resize event to trigger dimension recalculation
  const handleResizeStop = useCallback((_layout: Layout[], _oldItem: Layout, newItem: Layout) => {
    // Dispatch a custom event that the container can listen for
    window.dispatchEvent(new CustomEvent('chartResized', { 
      detail: { chartId: newItem.i } 
    }));
    
    // Also trigger a window resize event as a fallback
    setTimeout(() => {
      window.dispatchEvent(new Event('resize'));
    }, 50);
  }, []);

  return (
    <div className="h-screen flex flex-col bg-background">
      <div className="py-2 px-4 border-b border-border bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/90 flex justify-between items-center z-10">
        <div className="flex items-center gap-3">
          <Clock />
          <MarketStatus />
          <ApiStatus />
        </div>
        <WidgetMenu
          onAddChart={handleAddChart}
          onRemoveAll={handleRemoveAllCharts}
        />
      </div>

      <div className="flex-1 overflow-auto">
        <ResponsiveGridLayout
          className="layout"
          layouts={localLayouts}
          breakpoints={GRID_CONFIG.BREAKPOINTS}
          cols={GRID_CONFIG.COLS}
          rowHeight={GRID_CONFIG.ROW_HEIGHT}
          margin={[...GRID_CONFIG.MARGIN]}
          containerPadding={[...GRID_CONFIG.CONTAINER_PADDING]}
          onLayoutChange={onLayoutChange}
          onResizeStart={handleResizeStart}
          onResizeStop={handleResizeStop}
          maxRows={maxRows}
          isDraggable
          isResizable
          draggableHandle="[data-drag-handle]"
          useCSSTransforms
          compactType={null}
          preventCollision={true}
          autoSize={false}
          isBounded={false}
        >
          {charts.map(chart => {
            const layout = localLayouts.lg?.find(l => l.i === chart.id);
            return (
              <div 
                key={chart.id} 
                className="rounded-xl border border-border/80 bg-card overflow-hidden transition-colors"
                style={{ position: 'relative', height: '100%' }}
                data-chart-id={chart.id}
                data-grid={{
                  i: chart.id,
                  x: layout?.x ?? 0,
                  y: layout?.y ?? 0,
                  w: layout?.w ?? GRID_CONFIG.DEFAULT_CHART_WIDTH,
                  h: layout?.h ?? GRID_CONFIG.DEFAULT_CHART_HEIGHT,
                  minW: GRID_CONFIG.MIN_CHART_WIDTH,
                  minH: GRID_CONFIG.MIN_CHART_HEIGHT,
                }}
              >
                <ContextMenu onDelete={() => handleDeleteChart(chart.id)}>
                  <StockChartContainer 
                    chartId={chart.id}
                    initialSymbol={chart.symbol}
                    initialMultiplier={chart.multiplier}
                    initialTimespan={chart.timespan}
                    onSymbolChange={(symbol: string) => handleUpdateChart(chart.id, { symbol })}
                    onMultiplierChange={(multiplier: number) => handleUpdateChart(chart.id, { multiplier })}
                    onTimespanChange={(timespan: 'minute' | 'hour' | 'day' | 'week' | 'year') => 
                      handleUpdateChart(chart.id, { timespan })}
                  />
                </ContextMenu>
              </div>
            );
          })}
        </ResponsiveGridLayout>
      </div>
    </div>
  );
}
