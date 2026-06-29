export interface Trade {
  id: string;
  customerId: string;
  type: string;
  orderStatus: string;
  ticker: string;
  shares: number;
  openedAt: string;
  closedAt: string;
  entryPrice: number;
  closePrice: number;
  entryPosition: number;
  closePosition: number;
  profit: number;
}