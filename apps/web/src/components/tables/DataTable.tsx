import { formatDateTime } from '../../utils/dateFormatter';
import type { BarData } from '../../types/tools';

interface DataTableProps {
  data: BarData[];
}

export function DataTable({ data }: DataTableProps) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="bg-gray-50">
            <th className="px-4 py-2 text-left">Time</th>
            <th className="px-4 py-2 text-right">Open</th>
            <th className="px-4 py-2 text-right">High</th>
            <th className="px-4 py-2 text-right">Low</th>
            <th className="px-4 py-2 text-right">Close</th>
            <th className="px-4 py-2 text-right">Volume</th>
            <th className="px-4 py-2 text-right">VWAP</th>
            <th className="px-4 py-2 text-right">Trades</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-200">
          {data.map((bar, index) => (
            <tr key={index} className="hover:bg-gray-50">
              <td className="px-4 py-2">{formatDateTime(new Date(bar.t))}</td>
              <td className="px-4 py-2 text-right">${bar.o.toFixed(2)}</td>
              <td className="px-4 py-2 text-right">${bar.h.toFixed(2)}</td>
              <td className="px-4 py-2 text-right">${bar.l.toFixed(2)}</td>
              <td className="px-4 py-2 text-right">${bar.c.toFixed(2)}</td>
              <td className="px-4 py-2 text-right">{bar.v.toLocaleString()}</td>
              <td className="px-4 py-2 text-right">${bar.vw.toFixed(2)}</td>
              <td className="px-4 py-2 text-right">{bar.n}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}