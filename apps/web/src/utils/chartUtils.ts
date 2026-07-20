import { Trade } from '../types/trade';

export const chartOptions = {
  layout: {
    background: { color: '#ffffff' },
    textColor: '#64748b',
  },
  grid: {
    vertLines: { color: 'rgba(15, 23, 42, 0.06)' },
    horzLines: { color: 'rgba(15, 23, 42, 0.06)' },
  },
  crosshair: {
    mode: 0,
    vertLine: {
      width: 1 as 1,
      color: 'rgba(15, 23, 42, 0.3)',
      style: 0,
    },
    horzLine: {
      width: 1 as 1,
      color: 'rgba(15, 23, 42, 0.3)',
      style: 0,
    },
  },
  timeScale: {
    timeVisible: true,
    secondsVisible: false,
  },
  rightPriceScale: {
    borderColor: 'rgba(15, 23, 42, 0.15)',
  },
};

export const seriesOptions = {
  lineColor: '#14a3bd',
  topColor: 'rgba(20, 163, 189, 0.25)',
  bottomColor: 'rgba(20, 163, 189, 0)',
  lineWidth: 2 as 1 | 2 | 3 | 4,
};

export const formatPrice = (price: number) => {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  }).format(price);
};

export const formatProfit = (profit: number) => {
  const formatted = formatPrice(Math.abs(profit));
  return profit >= 0 ? `+${formatted}` : `-${formatted}`;
};

export const mockTrades: Trade[] = [
  {
    id: "bc93bbce-99f2-4651-8809-1b3452df2992",
    closedAt: "2025-01-27T13:39:09.2879948-05:00",
    closePosition: 947.04504,
    closePrice: 14.135,
    customerId: "efd517a3-5b49-42c0-abf0-7a69da2e40b9",
    entryPosition: 998.635,
    entryPrice: 14.905,
    openedAt: "2025-01-27T11:01:04.4230865-05:00",
    orderStatus: "Closed",
    profit: -51.589966,
    shares: 67,
    ticker: "SOUN",
    type: "Paper"
  },
  {
    id: "0901d0d5-ea64-413c-bdbf-1efeda133958",
    closedAt: "2025-01-27T10:28:31.2272024-05:00",
    closePosition: 1862.4886,
    closePrice: 8.2411,
    customerId: "efd517a3-5b49-42c0-abf0-7a69da2e40b9",
    entryPosition: 998.92004,
    entryPrice: 4.42,
    openedAt: "2025-01-27T10:28:15.7652184-05:00",
    orderStatus: "Closed",
    profit: 863.5686,
    shares: 226,
    ticker: "YIBO",
    type: "Paper"
  },
  {
    id: "58fd29f2-535e-4630-af38-e1228de0a588",
    closedAt: null,
    closePosition: 0,
    closePrice: 0,
    customerId: "efd517a3-5b49-42c0-abf0-7a69da2e40b9",
    entryPosition: 997.5828,
    entryPrice: 5.7999,
    openedAt: "2025-01-29T11:24-05:00",
    orderStatus: "Open",
    profit: 0,
    shares: 172,
    ticker: "SPXS",
    type: "Paper"
  }
];
