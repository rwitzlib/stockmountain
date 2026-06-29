import { useState } from 'react';

interface LocalDataFormProps {
  onSubmit: (data: {
    ticker: string;
    timespan: string;
  }) => void;
  isLoading?: boolean;
}

export function LocalDataForm({ onSubmit, isLoading }: LocalDataFormProps) {
  const [formData, setFormData] = useState({
    ticker: 'MARA',
    timespan: 'minute'
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit(formData);
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div>
          <label className="block text-sm font-medium text-gray-700">Ticker Symbol</label>
          <input
            type="text"
            value={formData.ticker}
            onChange={e => setFormData(prev => ({ ...prev, ticker: e.target.value.toUpperCase() }))}
            className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-blue-500 focus:ring-blue-500"
            required
          />
        </div>
        
        <div>
          <label className="block text-sm font-medium text-gray-700">Timespan</label>
          <select
            value={formData.timespan}
            onChange={e => setFormData(prev => ({ ...prev, timespan: e.target.value }))}
            className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-blue-500 focus:ring-blue-500"
          >
            <option value="minute">Minute</option>
            <option value="hour">Hour</option>
            <option value="day">Day</option>
          </select>
        </div>
      </div>

      <button
        type="submit"
        disabled={isLoading}
        className="w-full px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors disabled:bg-blue-400"
      >
        {isLoading ? 'Loading...' : 'Fetch Data'}
      </button>
    </form>
  );
}