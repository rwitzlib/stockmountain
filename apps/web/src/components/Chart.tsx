import { useEffect, useRef } from 'react';
import { createChart, IChartApi, SeriesMarker, Time } from 'lightweight-charts';
import { chartOptions, seriesOptions, mockTrades } from '../utils/chartUtils';

interface ChartProps {
  containerRef: React.RefObject<HTMLDivElement>;
}

export const Chart = ({ containerRef }: ChartProps) => {
  const chartRef = useRef<IChartApi | null>(null);

  useEffect(() => {
    if (!containerRef.current) return;

    const handleResize = () => {
      if (chartRef.current && containerRef.current) {
        chartRef.current.applyOptions({
          width: containerRef.current.clientWidth,
          height: containerRef.current.clientHeight,
        });
      }
    };

    chartRef.current = createChart(containerRef.current, {
      ...chartOptions,
      width: containerRef.current.clientWidth,
      height: containerRef.current.clientHeight,
    });

    const areaSeries = chartRef.current.addAreaSeries(seriesOptions);

    // Convert trade data to chart format and add markers
    const chartData = mockTrades.map(trade => ({
      time: new Date(trade.openedAt).getTime() / 1000 as Time,
      value: trade.entryPrice,
    }));

    const markers: SeriesMarker<Time>[] = mockTrades.map(trade => ({
      time: new Date(trade.openedAt).getTime() / 1000 as Time,
      position: 'belowBar',
      color: trade.profit >= 0 ? '#22c55e' : '#ef4444',
      shape: 'circle',
      text: `${trade.type} ${trade.orderStatus}`,
    }));

    areaSeries.setData(chartData);
    areaSeries.setMarkers(markers);

    window.addEventListener('resize', handleResize);

    return () => {
      window.removeEventListener('resize', handleResize);
      if (chartRef.current) {
        chartRef.current.remove();
      }
    };
  }, [containerRef]);

  return null;
};