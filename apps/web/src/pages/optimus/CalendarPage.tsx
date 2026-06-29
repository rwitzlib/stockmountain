import { useState, useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { Card } from '../../components/ui/card';
import { Button } from '../../components/ui/button';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Trade } from '../../types/trade';
import { formatPrice } from '../../utils/chartUtils';

// Days of the week header
const daysOfWeek = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

const CalendarPage = () => {
  const [currentDate, setCurrentDate] = useState(new Date());
  const [selectedDate, setSelectedDate] = useState<Date | undefined>(undefined);

  // Get the current month's data
  const currentYear = currentDate.getFullYear();
  const currentMonth = currentDate.getMonth();

  // Data fetching
  const { data: tradesData } = useQuery({
    queryKey: ['trades'],
    queryFn: async () => {
      const token = localStorage.getItem("accessToken");
      const headers: HeadersInit = {
        'Content-Type': 'application/json',
      };
      if (token) {
        headers['Authorization'] = `Bearer ${token}`;
      }

      const response = await fetch('https://api.stockmountain.io/api/trade?user=rob.witzlib@gmail.com', {
        headers: headers,
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

  // Calculate calendar data
  const calendarData = useMemo(() => {
    // Get the first day of the month
    const firstDay = new Date(currentYear, currentMonth, 1);
    // Get the last day of the month
    const lastDay = new Date(currentYear, currentMonth + 1, 0);
    
    // Get the day of the week for the first day (0-6, where 0 is Sunday)
    const firstDayOfWeek = firstDay.getDay();
    
    // Calculate how many days to show in the calendar grid
    const daysInMonth = lastDay.getDate();
    
    // Create an array of day numbers with empty slots for previous month days
    const days = Array(firstDayOfWeek).fill(null).concat(
      Array.from({ length: daysInMonth }, (_, i) => i + 1)
    );
    
    // Create rows for the calendar (6 rows max)
    const rows = [];
    let cells = [];
    
    days.forEach((day, i) => {
      if (i > 0 && i % 7 === 0) {
        rows.push(cells);
        cells = [];
      }
      cells.push(day);
    });
    
    // Add empty cells for the last row if needed
    while (cells.length < 7) {
      cells.push(null);
    }
    rows.push(cells);
    
    return rows;
  }, [currentYear, currentMonth]);

  // Function to get profit for a specific day
  const getDayProfit = (date: Date) => {
    return trades.reduce((total, trade) => {
      const tradeDate = new Date(trade.closedAt);
      if (
        tradeDate.getDate() === date.getDate() &&
        tradeDate.getMonth() === date.getMonth() &&
        tradeDate.getFullYear() === date.getFullYear()
      ) {
        return total + (trade.profit || 0);
      }
      return total;
    }, 0);
  };

  // Function to get trades for a specific day
  const getDayTrades = (date: Date): Trade[] => {
    return trades.filter(trade => {
      const tradeDate = new Date(trade.closedAt);
      return (
        tradeDate.getDate() === date.getDate() &&
        tradeDate.getMonth() === date.getMonth() &&
        tradeDate.getFullYear() === date.getFullYear()
      );
    });
  };

  // Function to navigate to previous month
  const goToPreviousMonth = () => {
    setCurrentDate(new Date(currentYear, currentMonth - 1, 1));
  };

  // Function to navigate to next month
  const goToNextMonth = () => {
    setCurrentDate(new Date(currentYear, currentMonth + 1, 1));
  };

  // When a day is selected
  const handleDayClick = (day: number | null) => {
    if (day === null) return;
    const selectedDate = new Date(currentYear, currentMonth, day);
    setSelectedDate(selectedDate);
  };

  const selectedDayTrades = selectedDate ? getDayTrades(selectedDate) : [];
  const selectedDayProfit = selectedDate ? getDayProfit(selectedDate) : 0;

  // Get color for a day based on profit
  const getDayColor = (day: number | null) => {
    if (day === null) return '';
    
    const date = new Date(currentYear, currentMonth, day);
    const profit = getDayProfit(date);
    
    if (profit > 0) return 'bg-green-100 hover:bg-green-200';
    if (profit < 0) return 'bg-red-100 hover:bg-red-200';
    return 'hover:bg-gray-100';
  };

  // Check if a day is selected
  const isSelected = (day: number | null) => {
    if (!day || !selectedDate) return false;
    
    return (
      selectedDate.getDate() === day &&
      selectedDate.getMonth() === currentMonth &&
      selectedDate.getFullYear() === currentYear
    );
  };

  // Function to check if a day is today
  const isToday = (day: number | null) => {
    if (day === null) return false;
    
    const today = new Date();
    return (
      today.getDate() === day &&
      today.getMonth() === currentMonth &&
      today.getFullYear() === currentYear
    );
  };

  // Format the month and year display
  const monthYearDisplay = currentDate.toLocaleDateString('en-US', {
    month: 'long',
    year: 'numeric'
  });

  return (
    <div className="min-h-screen bg-background p-4 md:p-8 pt-20 md:pt-8">
      <div className="max-w-7xl mx-auto space-y-12">
        <div className="flex items-center gap-4">
          <Link to="/optimus">
            <Button 
              variant="outline" 
              size="sm"
              className="bg-white/50 backdrop-blur-sm hover:bg-white/80 border-purple-200 hover:border-purple-300"
            >
              <ChevronLeft className="h-4 w-4 mr-1" />
              Back
            </Button>
          </Link>
          <h1 className="text-2xl font-semibold gradient-heading">Trading Calendar</h1>
        </div>

        <div className={`grid ${selectedDate ? 'grid-cols-1 md:grid-cols-2 gap-6' : 'place-items-center'}`}>
          <div className="flex justify-center items-start pt-4 w-full">
            {/* Custom Calendar Component */}
            <div className="bg-white/80 shadow-sm rounded-md border p-4 w-full max-w-md">
              {/* Calendar Header */}
              <div className="flex justify-between items-center mb-4">
                <button 
                  onClick={goToPreviousMonth}
                  className="p-1 rounded-full hover:bg-gray-100"
                >
                  <ChevronLeft className="h-5 w-5 text-gray-500" />
                </button>
                <h2 className="text-lg font-semibold">{monthYearDisplay}</h2>
                <button 
                  onClick={goToNextMonth}
                  className="p-1 rounded-full hover:bg-gray-100"
                >
                  <ChevronRight className="h-5 w-5 text-gray-500" />
                </button>
              </div>
              
              {/* Days of Week Header */}
              <div className="grid grid-cols-7 gap-1 mb-1">
                {daysOfWeek.map(day => (
                  <div key={day} className="text-center text-sm font-medium text-gray-500 py-1">
                    {day}
                  </div>
                ))}
              </div>
              
              {/* Calendar Grid */}
              <div className="grid grid-cols-7 gap-1">
                {calendarData.flat().map((day, index) => (
                  <button
                    key={index}
                    onClick={() => handleDayClick(day)}
                    disabled={day === null}
                    className={`
                      h-10 w-full rounded-md flex items-center justify-center
                      ${day === null ? 'text-gray-300' : 'text-gray-700'} 
                      ${isSelected(day) ? 'ring-2 ring-purple-500 font-bold' : ''}
                      ${isToday(day) ? 'border-2 border-blue-500 font-bold' : ''}
                      ${getDayColor(day)}
                      transition-colors duration-200
                    `}
                  >
                    {day}
                  </button>
                ))}
              </div>
            </div>
          </div>
          
          {/* Selected Day Detail */}
          {selectedDate && (
            <div className="space-y-4">
              <Card className="p-6 bg-white/80 backdrop-blur-sm border-purple-200">
                <h2 className="text-xl font-semibold mb-4 gradient-heading">
                  {selectedDate.toLocaleDateString('en-US', {
                    weekday: 'long',
                    year: 'numeric',
                    month: 'long',
                    day: 'numeric',
                  })}
                </h2>
                <div className="flex items-center justify-between mb-4">
                  <div className="text-lg font-medium">Daily P/L:</div>
                  <div className={`text-xl font-bold ${selectedDayProfit >= 0 ? 'text-green-500' : 'text-red-500'}`}>
                    {formatPrice(selectedDayProfit)}
                  </div>
                </div>
              </Card>
              
              <Card className="p-6 bg-white/80 backdrop-blur-sm border-purple-200">
                <div className="flex items-center justify-between mb-4">
                  <h3 className="text-lg font-medium">Trades</h3>
                  <div className="text-sm text-gray-500">
                    {selectedDayTrades.length} {selectedDayTrades.length === 1 ? 'trade' : 'trades'}
                  </div>
                </div>
                
                {selectedDayTrades.length > 0 ? (
                  <div className="space-y-3 max-h-[350px] overflow-y-auto pr-2">
                    {selectedDayTrades.map((trade) => (
                      <div
                        key={trade.id}
                        className="p-3 rounded-md border bg-white shadow-sm hover:shadow-md transition-shadow"
                      >
                        <div className="flex justify-between items-center mb-2">
                          <span className="font-bold text-lg">{trade.ticker}</span>
                          <span
                            className={`text-lg font-bold ${
                              trade.profit >= 0 ? 'text-green-500' : 'text-red-500'
                            }`}
                          >
                            {formatPrice(trade.profit)}
                          </span>
                        </div>
                        <div className="flex justify-between text-sm text-gray-600">
                          <div>
                            <div>Entry: {new Date(trade.openedAt).toLocaleTimeString()}</div>
                            <div>Exit: {new Date(trade.closedAt).toLocaleTimeString()}</div>
                          </div>
                          <div className="text-right">
                            <div>Shares: {trade.shares}</div>
                            <div>Price: {formatPrice(trade.entryPrice)} → {formatPrice(trade.closePrice)}</div>
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                ) : (
                  <div className="flex flex-col items-center justify-center py-8 text-gray-500">
                    <p className="text-muted-foreground mb-2">No trades on this day</p>
                    <p className="text-sm">Select another date to view trades</p>
                  </div>
                )}
              </Card>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default CalendarPage;