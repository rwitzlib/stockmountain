
import { useState } from 'react';
import { MarketDataForm } from '../../../components/forms/MarketDataForm';
import { LocalDataForm } from '../../../components/forms/LocalDataForm';
import { fetchMarketData } from '../../../services/polygon';
import { fetchLocalMarketData } from '../../../services/local';
import type { StockMarketData } from '../../../types/tools';

interface DataInputProps {
  onDataSubmit: (data: StockMarketData) => void;
}

interface FetchDataParams {
  ticker: string;
  multiplier: number;
  timespan: 'minute' | 'hour' | 'day' | 'week' | 'month' | 'quarter' | 'year';
  from: string;
  to: string;
}

type InputMethod = 'manual' | 'polygon' | 'local';

export function DataInput({ onDataSubmit }: DataInputProps) {
  const [inputMethod, setInputMethod] = useState<InputMethod>('polygon');
  const [input, setInput] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleManualSubmit = () => {
    try {
      const parsedData = JSON.parse(input);
      
      // Transform the input data to match the expected StockMarketData format
      // Now checking for both uppercase and lowercase keys
      const getKeyInsensitive = (obj: any, key: string) => {
        const lowerKey = key.toLowerCase();
        const foundKey = Object.keys(obj).find(k => k.toLowerCase() === lowerKey);
        return foundKey ? obj[foundKey] : undefined;
      };

      const results = getKeyInsensitive(parsedData, 'Results') || getKeyInsensitive(parsedData, 'results') || [];
      
      const transformedData: StockMarketData = {
        ticker: getKeyInsensitive(parsedData, 'Ticker') || getKeyInsensitive(parsedData, 'ticker') || '',
        queryCount: 0,
        resultsCount: results.length,
        adjusted: true,
        results: results.map((result: any) => ({
          v: getKeyInsensitive(result, 'v') || getKeyInsensitive(result, 'V') || 0,
          vw: getKeyInsensitive(result, 'vw') || getKeyInsensitive(result, 'VW') || 0,
          o: getKeyInsensitive(result, 'o') || getKeyInsensitive(result, 'O') || 0,
          c: getKeyInsensitive(result, 'c') || getKeyInsensitive(result, 'C') || 0,
          h: getKeyInsensitive(result, 'h') || getKeyInsensitive(result, 'H') || 0,
          l: getKeyInsensitive(result, 'l') || getKeyInsensitive(result, 'L') || 0,
          t: getKeyInsensitive(result, 't') || getKeyInsensitive(result, 'T') || 0,
          n: getKeyInsensitive(result, 'n') || getKeyInsensitive(result, 'N') || 0
        })),
        status: getKeyInsensitive(parsedData, 'Status') || getKeyInsensitive(parsedData, 'status') || '',
        request_id: '',
        count: results.length
      };

      setError('');
      onDataSubmit(transformedData);
    } catch (e) {
      setError('Invalid JSON format');
    }
  };

  const handlePolygonFetch = async (formData: {
    ticker: string;
    multiplier: number;
    timespan: string;
    startDate: string;
    endDate: string;
  }) => {
    try {
      setIsLoading(true);
      setError('');
      const params: FetchDataParams = {
        ...formData,
        timespan: formData.timespan as 'minute' | 'hour' | 'day' | 'week' | 'month' | 'quarter' | 'year',
        from: formData.startDate,
        to: formData.endDate
      };
      const data = await fetchMarketData(params);
      onDataSubmit(data);
    } catch (e) {
      setError('Failed to fetch market data from Polygon');
    } finally {
      setIsLoading(false);
    }
  };

  const handleLocalFetch = async (formData: {
    ticker: string;
    timespan: string;
  }) => {
    try {
      setIsLoading(true);
      setError('');
      const data = await fetchLocalMarketData(formData);
      onDataSubmit(data);
    } catch (e) {
      setError('Failed to fetch market data from local API');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex justify-center">
        <div className="inline-flex rounded-lg border border-gray-200 p-1">
          <button
            onClick={() => setInputMethod('polygon')}
            className={`px-4 py-2 rounded-md text-sm font-medium transition-colors ${
              inputMethod === 'polygon'
                ? 'bg-blue-100 text-blue-700'
                : 'text-gray-500 hover:text-gray-700'
            }`}
          >
            Polygon API
          </button>
          <button
            onClick={() => setInputMethod('local')}
            className={`px-4 py-2 rounded-md text-sm font-medium transition-colors ${
              inputMethod === 'local'
                ? 'bg-blue-100 text-blue-700'
                : 'text-gray-500 hover:text-gray-700'
            }`}
          >
            Local API
          </button>
          <button
            onClick={() => setInputMethod('manual')}
            className={`px-4 py-2 rounded-md text-sm font-medium transition-colors ${
              inputMethod === 'manual'
                ? 'bg-blue-100 text-blue-700'
                : 'text-gray-500 hover:text-gray-700'
            }`}
          >
            Manual JSON
          </button>
        </div>
      </div>

      {error && (
        <div className="bg-red-50 text-red-700 p-3 rounded-lg text-sm">
          {error}
        </div>
      )}

      <div className="bg-white rounded-lg shadow-sm p-6">
        {inputMethod === 'polygon' && (
          <>
            <h2 className="text-lg font-semibold mb-4">Fetch from Polygon.io</h2>
            <MarketDataForm onSubmit={handlePolygonFetch} isLoading={isLoading} />
          </>
        )}
        
        {inputMethod === 'local' && (
          <>
            <h2 className="text-lg font-semibold mb-4">Fetch from Local API</h2>
            <LocalDataForm onSubmit={handleLocalFetch} isLoading={isLoading} />
          </>
        )}
        
        {inputMethod === 'manual' && (
          <>
            <h2 className="text-lg font-semibold mb-4">Manual JSON Input</h2>
            <div className="space-y-4">
              <textarea
                value={input}
                onChange={(e) => setInput(e.target.value)}
                className="w-full h-64 p-3 border rounded-lg font-mono text-sm"
                placeholder='Paste your JSON data here... Example format:
{
  "Ticker": "AAPL",
  "Status": "OK",
  "Results": [
    {
      "c": 189.95,
      "h": 191.52,
      "l": 189.8,
      "n": 2918,
      "o": 191.52,
      "t": 1704186000000,
      "v": 78491,
      "vw": 190.0364
    }
  ]
}'
              />
              <button
                onClick={handleManualSubmit}
                className="w-full px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors"
              >
                Analyze Data
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
