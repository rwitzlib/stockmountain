import { useState } from 'react';
import { ArgumentConfigForm } from '../components/forms/strategy/ArgumentConfigForm';
import { ScanArgument } from '../types/strategy';

export function ScannerPage() {
  const [filters, setFilters] = useState<ScanArgument>({
    operator: 'AND',
    filters: []
  });

  const handleSubmit = () => {
    // This will be implemented later to connect to your scanner API
    console.log('Scanner filters:', filters);
  };

  return (
    <div className="min-h-screen bg-gray-100 p-6">
      <div className="max-w-7xl mx-auto space-y-6">
        <div className="flex items-center justify-between">
          <h1 className="text-3xl font-bold text-gray-900">Stock Scanner</h1>
          <button
            onClick={handleSubmit}
            className="px-6 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors"
          >
            Run Scan
          </button>
        </div>

        <div className="bg-white rounded-lg shadow-md p-6">
          <ArgumentConfigForm
            value={filters}
            onChange={setFilters}
          />
        </div>

        {/* Results section will be added here later */}
      </div>
    </div>
  );
}