import { useState } from 'react';
import { Trash2, GripVertical, ChevronDown, ChevronUp } from 'lucide-react';
import { FilterComposer } from '../../filters/FilterComposer';
import { Button } from '../../ui/button';
import type { EntrySettings } from '../../../types/strategy';

interface EntrySettingsFormProps {
  value: EntrySettings;
  onChange: (value: EntrySettings) => void;
}

export function EntrySettingsForm({ value, onChange }: EntrySettingsFormProps) {
  const [isComposerExpanded, setIsComposerExpanded] = useState(true);

  const handleAddFilter = (expression: string) => {
    onChange({
      ...value,
      filters: [...value.filters, expression],
    });
  };

  const handleRemoveFilter = (index: number) => {
    onChange({
      ...value,
      filters: value.filters.filter((_, i) => i !== index),
    });
  };

  const handleMoveFilter = (index: number, direction: 'up' | 'down') => {
    const newIndex = direction === 'up' ? index - 1 : index + 1;
    if (newIndex < 0 || newIndex >= value.filters.length) return;

    const newFilters = [...value.filters];
    [newFilters[index], newFilters[newIndex]] = [newFilters[newIndex], newFilters[index]];
    onChange({ ...value, filters: newFilters });
  };

  const handleEditFilter = (index: number, newExpression: string) => {
    onChange({
      ...value,
      filters: value.filters.map((f, i) => (i === index ? newExpression : f)),
    });
  };

  return (
    <div className="space-y-6">
      {/* Active Filters List */}
      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <div>
            <h4 className="text-sm font-medium text-foreground">Entry Conditions</h4>
            <p className="text-xs text-muted-foreground">
              {value.filters.length === 0 
                ? 'Add filters to define when to enter positions'
                : `${value.filters.length} filter${value.filters.length !== 1 ? 's' : ''} configured (combined with AND)`
              }
            </p>
          </div>
          {value.filters.length > 0 && (
            <span className="rounded-full border border-border px-2 py-0.5 text-[10px] font-medium text-muted-foreground tabular-nums">
              {value.filters.length}
            </span>
          )}
        </div>

        {value.filters.length > 0 && (
          <div className="space-y-2">
            {value.filters.map((filter, index) => (
              <div
                key={`${filter}-${index}`}
                className="group flex items-center gap-2 p-3 rounded-lg bg-muted/30 border border-border hover:bg-accent/40 transition-colors"
              >
                {/* Drag Handle / Index */}
                <div className="flex items-center gap-1 text-muted-foreground">
                  <GripVertical className="w-4 h-4 opacity-0 group-hover:opacity-50" />
                  <span className="text-xs font-mono w-5">{index + 1}.</span>
                </div>

                {/* Filter Expression */}
                <div className="flex-1 min-w-0">
                  <input
                    type="text"
                    value={filter}
                    onChange={(e) => handleEditFilter(index, e.target.value)}
                    className="w-full bg-transparent border-none outline-none text-sm font-mono text-foreground placeholder:text-muted-foreground"
                    placeholder="Enter filter expression..."
                  />
                </div>

                {/* Actions */}
                <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    onClick={() => handleMoveFilter(index, 'up')}
                    disabled={index === 0}
                    className="h-7 w-7 p-0 text-muted-foreground hover:text-foreground"
                  >
                    <ChevronUp className="w-4 h-4" />
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    onClick={() => handleMoveFilter(index, 'down')}
                    disabled={index === value.filters.length - 1}
                    className="h-7 w-7 p-0 text-muted-foreground hover:text-foreground"
                  >
                    <ChevronDown className="w-4 h-4" />
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    onClick={() => handleRemoveFilter(index)}
                    className="h-7 w-7 p-0 text-muted-foreground hover:text-red-500"
                  >
                    <Trash2 className="w-4 h-4" />
                  </Button>
                </div>
              </div>
            ))}
          </div>
        )}

        {value.filters.length === 0 && (
          <div className="p-6 rounded-lg border-2 border-dashed border-border text-center">
            <p className="text-sm text-muted-foreground">
              No entry conditions configured yet.
            </p>
            <p className="text-xs text-muted-foreground mt-1">
              Use the filter builder below to add conditions.
            </p>
          </div>
        )}
      </div>

      {/* Filter Composer */}
      <div className="space-y-3">
        <button
          type="button"
          onClick={() => setIsComposerExpanded(!isComposerExpanded)}
          className="w-full flex items-center justify-between p-3 rounded-lg bg-muted/30 border border-border hover:bg-accent transition-colors"
        >
          <div className="flex items-center gap-2">
            <span className="text-sm font-medium text-foreground">Filter Builder</span>
            <span className="text-xs text-muted-foreground">
              Build expressions with indicators, operators, and timeframes
            </span>
          </div>
          {isComposerExpanded ? (
            <ChevronUp className="w-4 h-4 text-muted-foreground" />
          ) : (
            <ChevronDown className="w-4 h-4 text-muted-foreground" />
          )}
        </button>

        {isComposerExpanded && (
          <div className="p-4 rounded-lg border border-border bg-card">
            <FilterComposer
              onAddFilter={handleAddFilter}
              addButtonLabel="Add Entry Condition"
              allowCode={true}
              initialMode="builder"
            />
          </div>
        )}
      </div>

      {/* Quick Examples */}
      <div className="space-y-2">
        <p className="text-xs font-medium text-muted-foreground">Quick Add Examples:</p>
        <div className="flex flex-wrap gap-2">
          {[
            'adv() > 2000000 [1d]',
            'close > sma(50) [1d]',
            'rsi(14) < 30 [1d]',
            'macd(12,26,9) > 0 [1d]',
          ].map((example) => (
            <button
              key={example}
              type="button"
              onClick={() => handleAddFilter(example)}
              className="rounded-md border border-border/60 bg-muted/50 px-2.5 py-1.5 font-mono text-xs hover:bg-accent hover:text-foreground transition-colors"
            >
              {example}
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}

