import { useState } from 'react';
import { ChevronDown, ChevronRight, Trash2 } from 'lucide-react';
import { Filter, ScanArgument } from '../../../types/strategy';
import { FilterForm } from '../strategy/FilterForm';
import { Badge } from '../../ui/badge';

interface ArgumentConfigFormProps {
  value: ScanArgument;
  onChange: (value: ArgumentConfigFormProps['value']) => void;
}

export function ArgumentConfigForm({ value, onChange }: ArgumentConfigFormProps) {
  const [isExpanded, setIsExpanded] = useState(true);
  const [expandedFilters, setExpandedFilters] = useState<number[]>([]);

  const handleAddFilter = () => {
    const newFilterIndex = value.filters.length;
    onChange({
      ...value,
      filters: [...value.filters, {
        collectionModifier: 'ANY',
        firstOperand: {
          type: 'Study',
          name: 'rsi',
          parameters: '14,70,30,ema',
          modifier: 'Value',
          timeframe: {
            multiplier: 1,
            timespan: 'minute'
          }
        },
        operator: 'gt',
        secondOperand: {
          type: 'Fixed',
          value: 0
        },
        timeframe: {
          multiplier: 1,
          timespan: 'minute'
        }
      }]
    });
    // Expand the newly added filter
    setExpandedFilters(prev => [...prev, newFilterIndex]);
  };

  const handleRemoveFilter = (index: number) => {
    onChange({
      ...value,
      filters: value.filters.filter((_, i) => i !== index)
    });
  };

  const handleFilterChange = (index: number, filter: Filter) => {
    onChange({
      ...value,
      filters: value.filters.map((f, i) => i === index ? filter : f)
    });
  };

  const toggleFilterExpanded = (index: number) => {
    setExpandedFilters(prev => 
      prev.includes(index) 
        ? prev.filter(i => i !== index)
        : [...prev, index]
    );
  };

  return (
    <div className="space-y-3">
      <div 
        className="w-full flex items-center justify-between text-[10px] font-mono uppercase tracking-wider text-gray-500 hover:bg-gray-900/50 transition-colors p-2 cursor-pointer border border-gray-800"
        onClick={() => setIsExpanded(!isExpanded)}
      >
        <div className="flex items-center gap-2">
          {isExpanded ? (
            <ChevronDown className="w-3 h-3 text-cyan-400" />
          ) : (
            <ChevronRight className="w-3 h-3 text-cyan-400" />
          )}
          <span>:: Entry Conditions</span>
          {value.filters.length > 0 && (
            <span className="ml-2">
              <Badge variant="secondary" className="text-[9px] font-mono bg-cyan-950 text-cyan-400 border-cyan-800">{value.filters.length}</Badge>
            </span>
          )}
        </div>
        {!isExpanded && (
          <div onClick={e => e.stopPropagation()}>
            <button
              type="button"
              onClick={handleAddFilter}
              className="px-3 py-1 text-xs bg-green-950 text-green-400 border border-green-700 hover:bg-green-900 transition-all font-mono uppercase"
            >
              + Filter
            </button>
          </div>
        )}
      </div>

      {isExpanded && (
        <div className="space-y-3 pt-2">
          <div className="flex items-center justify-between gap-4 flex-wrap">
            <div className="space-y-2">
              <div className="text-[10px] font-mono uppercase text-gray-600">Combine With:</div>
              <div className="inline-grid grid-cols-2 gap-1 p-0.5 border border-gray-800 bg-gray-950">
                <button
                  type="button"
                  onClick={() => onChange({ ...value, operator: 'AND' })}
                  className={`px-3 py-1 font-mono text-xs uppercase transition-all ${value.operator === 'AND' ? 'bg-emerald-950 text-emerald-400 border border-emerald-700' : 'text-gray-600 hover:text-emerald-400'}`}
                >
                  AND
                </button>
                <button
                  type="button"
                  onClick={() => onChange({ ...value, operator: 'OR' })}
                  className={`px-3 py-1 font-mono text-xs uppercase transition-all ${value.operator === 'OR' ? 'bg-emerald-950 text-emerald-400 border border-emerald-700' : 'text-gray-600 hover:text-emerald-400'}`}
                >
                  OR
                </button>
              </div>
            </div>
            <button
              type="button"
              onClick={handleAddFilter}
              className="px-4 py-2 bg-green-950 text-green-400 border border-green-700 hover:bg-green-900 hover:border-green-500 transition-all font-mono text-xs uppercase"
            >
              + Add Filter
            </button>
          </div>

          <div className="space-y-3">
            {value.filters.map((filter, index) => {
              const isFilterExpanded = expandedFilters.includes(index);
              return (
                <div key={index} className="bg-gray-950/50 border border-gray-800 overflow-hidden">
                  <div 
                    className="p-3 cursor-pointer hover:bg-gray-900/50 transition-colors flex items-center justify-between"
                    onClick={() => toggleFilterExpanded(index)}
                  >
                    <div className="flex items-center gap-3">
                      {isFilterExpanded ? (
                        <ChevronDown className="w-3 h-3 text-cyan-400" />
                      ) : (
                        <ChevronRight className="w-3 h-3 text-cyan-400" />
                      )}
                      <span className="text-xs font-mono uppercase text-gray-400">
                        Filter #{index + 1}
                      </span>
                      {!isFilterExpanded && (
                        <span className="text-xs text-gray-600 font-mono">
                          {`${filter.firstOperand?.type || ''} ${filter.operator?.toUpperCase?.() || ''} ${filter.secondOperand?.type || ''}`}
                        </span>
                      )}
                    </div>
                    <button
                      type="button"
                      onClick={(e) => {
                        e.stopPropagation();
                        handleRemoveFilter(index);
                      }}
                      className="flex items-center gap-1 px-2 py-1 text-red-400 hover:bg-red-950/30 hover:border-red-700 border border-transparent transition-all font-mono text-xs uppercase"
                    >
                      <Trash2 className="w-3 h-3" />
                      <span>Delete</span>
                    </button>
                  </div>
                  
                  {isFilterExpanded && (
                    <div className="border-t border-gray-800 p-3">
                      <FilterForm
                        value={filter}
                        onChange={filter => handleFilterChange(index, filter)}
                      />
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}