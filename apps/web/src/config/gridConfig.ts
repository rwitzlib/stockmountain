import type { Layout } from 'react-grid-layout';

export const GRID_CONFIG = {
  DEFAULT_CHART_HEIGHT: 5, // 5 units = 250px (with ROW_HEIGHT of 50)
  MIN_CHART_WIDTH: 2,
  MIN_CHART_HEIGHT: 3, // Minimum 150px (3 * 50px)
  DEFAULT_CHART_WIDTH: 6,
  BREAKPOINTS: { lg: 1200, md: 996, sm: 768, xs: 480, xxs: 0 },
  COLS: { lg: 12, md: 10, sm: 6, xs: 4, xxs: 2 },
  ROW_HEIGHT: 50,
  MARGIN: [10, 10] as [number, number],
  CONTAINER_PADDING: [10, 10] as [number, number]
} as const;

export function createDefaultLayout(id: string): Layout {
  return {
    i: id,
    x: 0,
    y: 0,
    w: GRID_CONFIG.DEFAULT_CHART_WIDTH,
    h: GRID_CONFIG.DEFAULT_CHART_HEIGHT,
    minW: GRID_CONFIG.MIN_CHART_WIDTH,
    minH: GRID_CONFIG.MIN_CHART_HEIGHT,
    maxH: undefined,
    static: false
  };
}
