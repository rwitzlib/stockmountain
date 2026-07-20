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
        className="w-full flex items-center justify-between text-sm font-medium text-foreground hover:bg-accent transition-colors p-2 cursor-pointer rounded-lg border border-border/80"
        onClick={() => setIsExpanded(!isExpanded)}
      >
        <div className="flex items-center gap-2">
          {isExpanded ? (
            <ChevronDown className="w-3 h-3 text-muted-foreground" />
          ) : (
            <ChevronRight className="w-3 h-3 text-muted-foreground" />
          )}
          <span>Entry Conditions</span>
          {value.filters.length > 0 && (
            <span className="ml-2">
              <Badge variant="secondary" className="rounded-full border border-border px-2 py-0.5 text-[10px] font-medium text-muted-foreground bg-transparent">{value.filters.length}</Badge>
            </span>
          )}
        </div>
        {!isExpanded && (
          <div onClick={e => e.stopPropagation()}>
            <button
              type="button"
              onClick={handleAddFilter}
              className="px-3 py-1 text-xs font-medium rounded-lg border border-border bg-card text-foreground hover:bg-accent transition-colors"
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
              <div className="text-xs text-muted-foreground">Combine with</div>
              <div className="inline-grid grid-cols-2 gap-1 p-0.5 rounded-lg border border-border bg-muted/30">
                <button
                  type="button"
                  onClick={() => onChange({ ...value, operator: 'AND' })}
                  className={`px-3 py-1 text-xs rounded-md transition-colors ${value.operator === 'AND' ? 'bg-accent text-foreground font-medium' : 'text-muted-foreground hover:bg-accent hover:text-foreground'}`}
                >
                  AND
                </button>
                <button
                  type="button"
                  onClick={() => onChange({ ...value, operator: 'OR' })}
                  className={`px-3 py-1 text-xs rounded-md transition-colors ${value.operator === 'OR' ? 'bg-accent text-foreground font-medium' : 'text-muted-foreground hover:bg-accent hover:text-foreground'}`}
                >
                  OR
                </button>
              </div>
            </div>
            <button
              type="button"
              onClick={handleAddFilter}
              className="px-4 py-2 bg-primary text-primary-foreground rounded-lg text-xs font-medium hover:bg-primary/90 transition-colors"
            >
              + Add Filter
            </button>
          </div>

          <div className="space-y-3">
            {value.filters.map((filter, index) => {
              const isFilterExpanded = expandedFilters.includes(index);
              return (
                <div key={index} className="rounded-xl border border-border/80 bg-card overflow-hidden">
                  <div 
                    className="p-3 cursor-pointer hover:bg-accent/40 transition-colors flex items-center justify-between"
                    onClick={() => toggleFilterExpanded(index)}
                  >
                    <div className="flex items-center gap-3">
                      {isFilterExpanded ? (
                        <ChevronDown className="w-3 h-3 text-muted-foreground" />
                      ) : (
                        <ChevronRight className="w-3 h-3 text-muted-foreground" />
                      )}
                      <span className="text-xs font-medium text-foreground">
                        Filter {index + 1}
                      </span>
                      {!isFilterExpanded && (
                        <span className="text-xs text-muted-foreground font-mono">
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
                      className="flex items-center gap-1 px-2 py-1 rounded-md text-red-600 dark:text-red-400 hover:bg-accent transition-colors text-xs"
                    >
                      <Trash2 className="w-3 h-3" />
                      <span>Delete</span>
                    </button>
                  </div>
                  
                  {isFilterExpanded && (
                    <div className="border-t border-border p-3">
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