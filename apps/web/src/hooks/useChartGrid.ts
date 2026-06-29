import { useState, useCallback, useEffect } from 'react';
import { Layout, Layouts } from 'react-grid-layout';
import { GRID_CONFIG, createDefaultLayout } from '../config/gridConfig';
import type { BarData } from '../types/tools';

interface Chart {
  id: string;
  symbol?: string;
  timeframe?: string;
  multiplier?: number;
  timespan?: 'minute' | 'hour' | 'day' | 'week' | 'year';
}

export function useChartGrid() {
  // Load initial charts from localStorage
  const [charts, setCharts] = useState<Chart[]>(() => {
    try {
      const savedCharts = localStorage.getItem('stockCharts');
      return savedCharts ? JSON.parse(savedCharts) : [];
    } catch (error) {
      console.error('Error loading saved charts:', error);
      return [];
    }
  });

  // Load initial layouts from localStorage
  const [layouts, setLayouts] = useState<Layouts>(() => {
    try {
      const savedLayouts = localStorage.getItem('stockChartLayouts');
      if (!savedLayouts) return {};
      
      const parsedLayouts = JSON.parse(savedLayouts);
      
      // Make sure all charts have valid layout entries
      const validLayouts: Layouts = {};
      
      // Process each breakpoint's layouts
      Object.keys(parsedLayouts).forEach(breakpoint => {
        validLayouts[breakpoint] = parsedLayouts[breakpoint].map((item: Layout) => ({
          i: item.i,
          x: typeof item.x === 'number' ? item.x : 0,
          y: typeof item.y === 'number' ? item.y : 0,
          w: Math.max(
            typeof item.w === 'number' ? item.w : GRID_CONFIG.DEFAULT_CHART_WIDTH,
            GRID_CONFIG.MIN_CHART_WIDTH
          ),
          h: Math.max(
            typeof item.h === 'number' ? item.h : GRID_CONFIG.DEFAULT_CHART_HEIGHT,
            GRID_CONFIG.MIN_CHART_HEIGHT
          ),
          minW: GRID_CONFIG.MIN_CHART_WIDTH,
          minH: GRID_CONFIG.MIN_CHART_HEIGHT,
        }));
      });
      
      return validLayouts;
    } catch (error) {
      console.error('Error loading saved layouts:', error);
      return {};
    }
  });

  const [maxRows, setMaxRows] = useState(12);
  
  // Updated max rows calculation based on window height
  useEffect(() => {
    const updateMaxRows = () => {
      const containerHeight = window.innerHeight - 50;
      const rowHeight = GRID_CONFIG.ROW_HEIGHT;
      const margin = GRID_CONFIG.MARGIN[1];
      const availableHeight = containerHeight - margin * 2;
      const calculatedMaxRows = Math.floor(availableHeight / (rowHeight + margin));
      setMaxRows(calculatedMaxRows);
    };

    updateMaxRows();
    window.addEventListener('resize', updateMaxRows);
    return () => window.removeEventListener('resize', updateMaxRows);
  }, []);

  // Save charts to localStorage whenever they change
  useEffect(() => {
    try {
      localStorage.setItem('stockCharts', JSON.stringify(charts));
    } catch (error) {
      console.error('Error saving charts:', error);
    }
  }, [charts]);

  // Save layouts to localStorage whenever they change
  useEffect(() => {
    try {
      localStorage.setItem('stockChartLayouts', JSON.stringify(layouts));
    } catch (error) {
      console.error('Error saving layouts:', error);
    }
  }, [layouts]);

  // Make sure layout changes maintain correct structure
  const handleLayoutChange = useCallback((currentLayout: Layout[], allLayouts: Layouts) => {
    // Ensure layout changes maintain minimum constraints
    const updatedLayouts = Object.keys(allLayouts).reduce((acc, breakpoint) => {
      acc[breakpoint] = allLayouts[breakpoint].map(item => ({
        ...item,
        minW: GRID_CONFIG.MIN_CHART_WIDTH,
        minH: GRID_CONFIG.MIN_CHART_HEIGHT,
        h: Math.max(item.h, GRID_CONFIG.MIN_CHART_HEIGHT),
        w: Math.max(item.w, GRID_CONFIG.MIN_CHART_WIDTH),
      }));
      return acc;
    }, {} as Layouts);

    setLayouts(updatedLayouts);
  }, []);

  const handleAddChart = useCallback(() => {
    const newChartId = `chart-${Date.now()}`;

    setCharts(prevCharts => [...prevCharts, { id: newChartId, symbol: 'SPY' }]);

    // Create initial layout for the new chart
    setLayouts(prevLayouts => {
      const newLayouts = { ...prevLayouts };

      Object.keys(GRID_CONFIG.BREAKPOINTS).forEach(breakpoint => {
        if (!newLayouts[breakpoint]) {
          newLayouts[breakpoint] = [];
        }

        // Calculate the y position for the new chart by finding the maximum y + h of existing charts
        const existingLayouts = newLayouts[breakpoint];
        let maxY = 0;
        existingLayouts.forEach(layout => {
          const bottomY = layout.y + layout.h;
          if (bottomY > maxY) {
            maxY = bottomY;
          }
        });

        newLayouts[breakpoint].push({
          i: newChartId,
          x: 0,
          y: maxY,
          w: GRID_CONFIG.DEFAULT_CHART_WIDTH,
          h: GRID_CONFIG.DEFAULT_CHART_HEIGHT,
          minW: GRID_CONFIG.MIN_CHART_WIDTH,
          minH: GRID_CONFIG.MIN_CHART_HEIGHT,
        });
      });

      return newLayouts;
    });
  }, []);

  const handleDeleteChart = useCallback((chartId: string) => {
    setCharts(prevCharts => prevCharts.filter(chart => chart.id !== chartId));
    setLayouts(prevLayouts => {
      const newLayouts: Layouts = {};
      Object.keys(prevLayouts).forEach(breakpoint => {
        newLayouts[breakpoint] = prevLayouts[breakpoint].filter(
          layout => layout.i !== chartId
        );
      });
      return newLayouts;
    });
  }, []);

  const handleRemoveAllCharts = useCallback(() => {
    setCharts([]);
    setLayouts({});
  }, []);

  const handleSymbolChange = useCallback((id: string, symbol: string) => {
    setCharts(prevCharts => prevCharts.map(chart => 
      chart.id === id ? { ...chart, symbol } : chart
    ));
  }, []);

  const handleTimeframeChange = useCallback((id: string, timeframe: string) => {
    setCharts(prevCharts => prevCharts.map(chart => 
      chart.id === id ? { ...chart, timeframe } : chart
    ));
  }, []);

  const handleDataUpdate = useCallback((id: string, data: BarData[]) => {
    setCharts(prevCharts => prevCharts.map(chart => 
      chart.id === id ? { ...chart, data } : chart
    ));
  }, []);

  // New function to update chart properties
  const handleUpdateChart = useCallback((
    chartId: string, 
    properties: Partial<Omit<Chart, 'id'>>
  ) => {
    setCharts(prevCharts => {
      const updatedCharts = prevCharts.map(chart => 
        chart.id === chartId 
          ? { ...chart, ...properties }
          : chart
      );
      
      // Save charts to localStorage directly here for immediate persistence
      try {
        localStorage.setItem('stockCharts', JSON.stringify(updatedCharts));
      } catch (error) {
        console.error('Error saving charts:', error);
      }
      
      return updatedCharts;
    });
  }, []);

  return {
    charts,
    layouts,
    maxRows,
    handleLayoutChange,
    handleAddChart,
    handleDeleteChart,
    handleRemoveAllCharts,
    handleSymbolChange,
    handleTimeframeChange,
    handleDataUpdate,
    handleUpdateChart
  };
}
