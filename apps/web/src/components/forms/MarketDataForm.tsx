import { useState } from 'react';

interface MarketDataFormProps {
  onSubmit: (data: {
    ticker: string;
    multiplier: number;
    timespan: string;
    startDate: string;
    endDate: string;
  }) => void;
  isLoading?: boolean;
}

export function MarketDataForm({ onSubmit, isLoading }: MarketDataFormProps) {
  const [formData, setFormData] = useState({
    ticker: 'AAPL',
    multiplier: 1,
    timespan: 'day',
    startDate: '',
    endDate: ''
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit(formData);
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div>
          <label className="block text-sm font-medium text-foreground">Ticker Symbol</label>
          <input
            type="text"
            value={formData.ticker}
            onChange={e => setFormData(prev => ({ ...prev, ticker: e.target.value.toUpperCase() }))}
            className="mt-1 block w-full rounded-lg border border-input bg-card px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
            required
          />
        </div>
        
        <div>
          <label className="block text-sm font-medium text-foreground">Timespan</label>
          <select
            value={formData.timespan}
            onChange={e => setFormData(prev => ({ ...prev, timespan: e.target.value }))}
            className="mt-1 block w-full rounded-lg border border-input bg-card px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
          >
            <option value="minute">Minute</option>
            <option value="hour">Hour</option>
            <option value="day">Day</option>
            <option value="week">Week</option>
            <option value="month">Month</option>
          </select>
        </div>

        <div>
          <label className="block text-sm font-medium text-foreground">Start Date</label>
          <input
            type="date"
            value={formData.startDate}
            onChange={e => setFormData(prev => ({ ...prev, startDate: e.target.value }))}
            className="mt-1 block w-full rounded-lg border border-input bg-card px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
            required
          />
        </div>

        <div>
          <label className="block text-sm font-medium text-foreground">End Date</label>
          <input
            type="date"
            value={formData.endDate}
            onChange={e => setFormData(prev => ({ ...prev, endDate: e.target.value }))}
            className="mt-1 block w-full rounded-lg border border-input bg-card px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
            required
          />
        </div>
      </div>

      <button
        type="submit"
        disabled={isLoading}
        className="w-full px-4 py-2 bg-primary text-primary-foreground font-medium rounded-lg hover:bg-primary/90 transition-colors disabled:opacity-50"
      >
        {isLoading ? 'Loading...' : 'Fetch Data'}
      </button>
    </form>
  );
}