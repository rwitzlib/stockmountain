import React, { useState } from 'react';
import { Camera } from 'lucide-react';

interface SnapshotEntry {
  ticker: string;
  results: Array<{
    timestamp: number;
    dateTime: string;
    minute?: {
      c: number;
      h: number;
      l: number;
      n: number;
      o: number;
      otc: boolean;
      t: number;
      v: number;
      vw: number;
    };
    hour?: {
      c: number;
      h: number;
      l: number;
      n: number;
      o: number;
      otc: boolean;
      t: number;
      v: number;
      vw: number;
    };
  }>;
}

interface SnapshotData {
  entries: SnapshotEntry[];
}

export function SnapshotPage() {
  const [jsonInput, setJsonInput] = useState('');
  const [data, setData] = useState<SnapshotData | null>(null);
  const [error, setError] = useState('');

  const normalizeKeys = (obj: any): any => {
    if (Array.isArray(obj)) {
      return obj.map(normalizeKeys);
    } else if (obj !== null && typeof obj === 'object') {
      const normalized: any = {};
      for (const [key, value] of Object.entries(obj)) {
        const lowerKey = key.toLowerCase();
        normalized[lowerKey] = normalizeKeys(value);
      }
      return normalized;
    }
    return obj;
  };

  const mapToExpectedStructure = (normalizedData: any): SnapshotData => {
    // Handle the case where data might be directly an entries array or wrapped in an object
    let entriesArray;
    if (Array.isArray(normalizedData)) {
      entriesArray = normalizedData;
    } else if (normalizedData.entries) {
      entriesArray = normalizedData.entries;
    } else {
      // If it's a single entry object, wrap it in an array
      entriesArray = [normalizedData];
    }

    const mappedEntries = entriesArray.map((entry: any) => ({
      ticker: entry.ticker || entry.symbol || 'Unknown',
      results: (entry.results || []).map((result: any) => ({
        timestamp: result.timestamp || result.t || 0,
        dateTime: result.datetime || result.datetz || result.date || new Date(result.timestamp || result.t || 0).toISOString(),
        ...(result.minute && {
          minute: {
            c: result.minute.c || result.minute.close || 0,
            h: result.minute.h || result.minute.high || 0,
            l: result.minute.l || result.minute.low || 0,
            n: result.minute.n || result.minute.transactions || result.minute.count || 0,
            o: result.minute.o || result.minute.open || 0,
            otc: result.minute.otc || false,
            t: result.minute.t || result.minute.timestamp || 0,
            v: result.minute.v || result.minute.volume || 0,
            vw: result.minute.vw || result.minute.vwap || 0,
          }
        }),
        ...(result.hour && {
          hour: {
            c: result.hour.c || result.hour.close || 0,
            h: result.hour.h || result.hour.high || 0,
            l: result.hour.l || result.hour.low || 0,
            n: result.hour.n || result.hour.transactions || result.hour.count || 0,
            o: result.hour.o || result.hour.open || 0,
            otc: result.hour.otc || false,
            t: result.hour.t || result.hour.timestamp || 0,
            v: result.hour.v || result.hour.volume || 0,
            vw: result.hour.vw || result.hour.vwap || 0,
          }
        }),
      }))
    }));

    return { entries: mappedEntries };
  };

  const handleSubmit = () => {
    try {
      setError('');
      const rawData = JSON.parse(jsonInput);
      const normalizedData = normalizeKeys(rawData);
      const mappedData = mapToExpectedStructure(normalizedData);
      setData(mappedData);
    } catch (e) {
      setError('Invalid JSON format. Please check your input.');
    }
  };

  const formatDateTime = (timestamp: number) => {
    return new Date(timestamp).toLocaleString();
  };

  const formatCurrency = (value: number) => {
    return `$${value.toFixed(2)}`;
  };

  const formatNumber = (value: number) => {
    return value.toLocaleString();
  };

  return (
    <div className="p-6">
      <div className="flex items-center gap-3 mb-6">
        <div className="p-2 bg-accent rounded-lg">
          <Camera className="w-6 h-6 text-amber-600 dark:text-amber-400" />
        </div>
        <h1 className="text-3xl font-semibold tracking-tight text-foreground">Snapshot Tool</h1>
      </div>
      
      <div className="space-y-6">
        {/* Input Form */}
        <div className="rounded-xl border border-border/80 bg-card p-6">
          <h2 className="text-lg font-semibold tracking-tight mb-4">JSON Data Input</h2>
          <div className="space-y-4">
            <textarea
              value={jsonInput}
              onChange={(e) => setJsonInput(e.target.value)}
              className="w-full h-64 p-3 rounded-lg border border-input bg-card font-mono text-sm resize-y"
              placeholder='Paste your JSON data here in the bon.json format (case-insensitive):

Examples - All of these work:
{
  "entries": [...] 
}
or
{
  "ENTRIES": [...]
}
or
{
  "Entries": [...]
}

Sample structure:
{
  "entries": [
    {
      "ticker": "BON",
      "results": [
        {
          "timestamp": 1747920300000,
          "dateTime": "2025-05-22T09:25:00-04:00",
          "minute": {
            "c": 1.91,
            "h": 1.94,
            "l": 1.88,
            "n": 2050,
            "o": 1.94,
            "otc": false,
            "t": 1747920300000,
            "v": 314922,
            "vw": 1.9148
          }
        }
      ]
    }
  ]
}

Alternative field names supported:
- ticker/symbol
- c/close, h/high, l/low, o/open
- v/volume, vw/vwap
- n/transactions/count
- t/timestamp
- dateTime/datetime/date'
            />
            
            {error && (
              <div className="bg-red-100/50 dark:bg-red-950/30 text-red-700 dark:text-red-400 p-3 rounded-lg text-sm">
                {error}
              </div>
            )}
            
            <button
              onClick={handleSubmit}
              className="px-6 py-2 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90 transition-colors"
            >
              Parse & Display Data
            </button>
          </div>
        </div>

        {/* Data Display */}
        {data && (
          <div className="space-y-6">
            {data.entries.map((entry, entryIndex) => (
              <div key={entryIndex} className="rounded-xl border border-border/80 bg-card p-6">
                <h2 className="text-xl font-semibold tracking-tight mb-4 text-foreground">
                  <span className="font-mono">{entry.ticker}</span> - Market Data
                </h2>
                
                <div className="overflow-x-auto">
                  <table className="w-full border-collapse">
                    <thead>
                      <tr className="bg-muted/50">
                        <th className="border border-border px-4 py-2 text-left text-[11px] font-medium uppercase tracking-widest text-muted-foreground">Date/Time</th>
                        <th className="border border-border px-4 py-2 text-left text-[11px] font-medium uppercase tracking-widest text-muted-foreground">Timeframe</th>
                        <th className="border border-border px-4 py-2 text-right text-[11px] font-medium uppercase tracking-widest text-muted-foreground">Open</th>
                        <th className="border border-border px-4 py-2 text-right text-[11px] font-medium uppercase tracking-widest text-muted-foreground">High</th>
                        <th className="border border-border px-4 py-2 text-right text-[11px] font-medium uppercase tracking-widest text-muted-foreground">Low</th>
                        <th className="border border-border px-4 py-2 text-right text-[11px] font-medium uppercase tracking-widest text-muted-foreground">Close</th>
                        <th className="border border-border px-4 py-2 text-right text-[11px] font-medium uppercase tracking-widest text-muted-foreground">Volume</th>
                        <th className="border border-border px-4 py-2 text-right text-[11px] font-medium uppercase tracking-widest text-muted-foreground">VWAP</th>
                        <th className="border border-border px-4 py-2 text-right text-[11px] font-medium uppercase tracking-widest text-muted-foreground">Trades</th>
                      </tr>
                    </thead>
                    <tbody>
                      {entry.results.map((result, resultIndex) => (
                        <React.Fragment key={resultIndex}>
                          {result.minute && (
                            <tr className="hover:bg-accent">
                              <td className="border border-border px-4 py-2 text-sm">
                                {result.dateTime}
                              </td>
                              <td className="border border-border px-4 py-2 text-sm">
                                <span className="inline-flex items-center rounded-full border border-border px-2 py-0.5 text-[10px] font-medium uppercase tracking-wider text-muted-foreground">
                                  Minute
                                </span>
                              </td>
                              <td className="border border-border px-4 py-2 text-sm text-right tabular-nums">
                                {formatCurrency(result.minute.o)}
                              </td>
                              <td className="border border-border px-4 py-2 text-sm text-right tabular-nums text-green-600 dark:text-green-400">
                                {formatCurrency(result.minute.h)}
                              </td>
                              <td className="border border-border px-4 py-2 text-sm text-right tabular-nums text-red-600 dark:text-red-400">
                                {formatCurrency(result.minute.l)}
                              </td>
                              <td className="border border-border px-4 py-2 text-sm text-right tabular-nums font-medium">
                                {formatCurrency(result.minute.c)}
                              </td>
                              <td className="border border-border px-4 py-2 text-sm text-right tabular-nums">
                                {formatNumber(result.minute.v)}
                              </td>
                              <td className="border border-border px-4 py-2 text-sm text-right tabular-nums">
                                {formatCurrency(result.minute.vw)}
                              </td>
                              <td className="border border-border px-4 py-2 text-sm text-right tabular-nums">
                                {formatNumber(result.minute.n)}
                              </td>
                            </tr>
                          )}
                          {result.hour && (
                            <tr className="hover:bg-accent">
                              <td className="border border-border px-4 py-2 text-sm">
                                {result.dateTime}
                              </td>
                              <td className="border border-border px-4 py-2 text-sm">
                                <span className="inline-flex items-center rounded-full border border-border px-2 py-0.5 text-[10px] font-medium uppercase tracking-wider text-muted-foreground">
                                  Hour
                                </span>
                              </td>
                              <td className="border border-border px-4 py-2 text-sm text-right tabular-nums">
                                {formatCurrency(result.hour.o)}
                              </td>
                              <td className="border border-border px-4 py-2 text-sm text-right tabular-nums text-green-600 dark:text-green-400">
                                {formatCurrency(result.hour.h)}
                              </td>
                              <td className="border border-border px-4 py-2 text-sm text-right tabular-nums text-red-600 dark:text-red-400">
                                {formatCurrency(result.hour.l)}
                              </td>
                              <td className="border border-border px-4 py-2 text-sm text-right tabular-nums font-medium">
                                {formatCurrency(result.hour.c)}
                              </td>
                              <td className="border border-border px-4 py-2 text-sm text-right tabular-nums">
                                {formatNumber(result.hour.v)}
                              </td>
                              <td className="border border-border px-4 py-2 text-sm text-right tabular-nums">
                                {formatCurrency(result.hour.vw)}
                              </td>
                              <td className="border border-border px-4 py-2 text-sm text-right tabular-nums">
                                {formatNumber(result.hour.n)}
                              </td>
                            </tr>
                          )}
                        </React.Fragment>
                      ))}
                    </tbody>
                  </table>
                </div>
                
                {/* Summary Statistics */}
                <div className="mt-6 grid grid-cols-2 md:grid-cols-4 gap-4 p-4 bg-muted/50 rounded-lg">
                  <div>
                    <p className="text-sm text-muted-foreground">Total Records</p>
                    <p className="text-lg font-semibold">{entry.results.length}</p>
                  </div>
                  <div>
                    <p className="text-sm text-muted-foreground">Minute Records</p>
                    <p className="text-lg font-semibold">
                      {entry.results.filter(r => r.minute).length}
                    </p>
                  </div>
                  <div>
                    <p className="text-sm text-muted-foreground">Hour Records</p>
                    <p className="text-lg font-semibold">
                      {entry.results.filter(r => r.hour).length}
                    </p>
                  </div>
                  <div>
                    <p className="text-sm text-muted-foreground">Time Range</p>
                    <p className="text-lg font-semibold">
                      {entry.results.length > 0 ? 
                        `${entry.results.length} ${entry.results.length === 1 ? 'point' : 'points'}` : 
                        'N/A'
                      }
                    </p>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
} 