import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Trade } from '../../types/trade';
import { useQuery } from '@tanstack/react-query';
import { TradeStatistics } from '../../components/trades/TradeStatistics';
import { TradesTable } from '../../components/trades/TradesTable';
import { ToggleGroup, ToggleGroupItem } from '../../components/ui/toggle-group';
import { Button } from '../../components/ui/button';
import { ArrowLeft } from 'lucide-react';

const TradesPage = () => {
  const [sortConfig, setSortConfig] = useState<{
    key: keyof Trade | null;
    direction: 'asc' | 'desc';
  }>({
    key: 'openedAt',
    direction: 'desc'
  });

  const [tradeType, setTradeType] = useState<string>('all');

  const { data: tradesData, error } = useQuery({
    queryKey: ['trades'],
    queryFn: async () => {
      const response = await fetch('https://api.stockmountain.io/api/trade?user=rob.witzlib@gmail.com', {
      // const response = await fetch('http://localhost:5046/api/trade/efd517a3-5b49-42c0-abf0-7a69da2e40b9', {
        headers: {
          'Authorization': `Bearer ${localStorage.getItem('accessToken')}`
        }
      });

      if (!response.ok) {
        throw new Error('Network response was not ok');
      }
      const data = await response.json();
      return data as Trade[];
    },
    refetchInterval: 30000,
  });

  const trades = tradesData || [];

  const filteredTrades = trades.filter(trade => {
    if (tradeType === 'all') return true;
    return trade.type.toLowerCase() === tradeType;
  });

  const sortData = (key: keyof Trade) => {
    const direction = sortConfig.key === key && sortConfig.direction === 'asc' ? 'desc' : 'asc';
    setSortConfig({ key, direction });
  };

  const getSortedData = () => {
    if (!sortConfig.key) return filteredTrades;

    return [...filteredTrades].sort((a, b) => {
      if (a[sortConfig.key!] === null) return 1;
      if (b[sortConfig.key!] === null) return -1;

      let aValue = a[sortConfig.key!];
      let bValue = b[sortConfig.key!];

      if (sortConfig.key === 'openedAt' || sortConfig.key === 'closedAt') {
        aValue = new Date(aValue as string).getTime();
        bValue = new Date(bValue as string).getTime();
      }

      if (aValue < bValue) return sortConfig.direction === 'asc' ? -1 : 1;
      if (aValue > bValue) return sortConfig.direction === 'asc' ? 1 : -1;
      return 0;
    });
  };

  if (error) {
    return <div className="min-h-screen p-4 flex items-center justify-center">Error loading trades</div>;
  }

  return (
    <div className="min-h-screen bg-background p-4 md:p-8 pt-20 md:pt-8">
      <div className="max-w-7xl mx-auto space-y-8">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-4">
            <Link to="/optimus">
              <Button 
                variant="outline" 
                size="sm"
                className="bg-white/50 backdrop-blur-sm hover:bg-white/80 border-purple-200 hover:border-purple-300"
              >
                <ArrowLeft className="h-4 w-4 mr-1" />
                Back
              </Button>
            </Link>
            <h1 className="text-2xl font-semibold gradient-heading">All Trades</h1>
          </div>
          <ToggleGroup 
            type="single" 
            value={tradeType} 
            onValueChange={(value) => setTradeType(value || 'all')}
            className="bg-white/50 backdrop-blur-sm p-1 rounded-lg border border-purple-200"
          >
            <ToggleGroupItem value="all" aria-label="Show all trades" className="data-[state=on]:bg-purple-100 data-[state=on]:text-purple-700">
              All
            </ToggleGroupItem>
            <ToggleGroupItem value="paper" aria-label="Show paper trades" className="data-[state=on]:bg-purple-100 data-[state=on]:text-purple-700">
              Paper
            </ToggleGroupItem>
            <ToggleGroupItem value="live" aria-label="Show live trades" className="data-[state=on]:bg-purple-100 data-[state=on]:text-purple-700">
              Live
            </ToggleGroupItem>
          </ToggleGroup>
        </div>

        <TradeStatistics trades={filteredTrades} />
        <TradesTable 
          trades={getSortedData()} 
          sortConfig={sortConfig}
          onSort={sortData}
        />
      </div>
    </div>
  );
};

export default TradesPage;