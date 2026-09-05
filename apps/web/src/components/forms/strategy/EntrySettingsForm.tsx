import { useState, type DragEvent, type KeyboardEvent } from 'react';
import { Trash2, GripVertical, Pencil } from 'lucide-react';
import { FilterComposer } from '../../filters/FilterComposer';
import { FilterChips } from '../../filters/FilterChips';
import { Button } from '../../ui/button';
import type { EntrySettings } from '../../../types/strategy';

interface EntrySettingsFormProps {
  value: EntrySettings;
  onChange: (value: EntrySettings) => void;
  /** Which evaluator the filters are destined for — scopes autocomplete + validation. */
  context?: 'scan' | 'backtest' | 'chart';
}

const moveItem = <T,>(items: T[], from: number, to: number): T[] => {
  if (from === to || from < 0 || to < 0 || from >= items.length || to >= items.length) return items;
  const next = [...items];
  const [moved] = next.splice(from, 1);
  next.splice(to, 0, moved);
  return next;
};

export function EntrySettingsForm({ value, onChange, context = 'scan' }: EntrySettingsFormProps) {
  const [editingIndex, setEditingIndex] = useState<number | null>(null);
  // Drag state: which row is being dragged, which row it's currently hovering over.
  const [dragIndex, setDragIndex] = useState<number | null>(null);
  const [overIndex, setOverIndex] = useState<number | null>(null);
  // Only a mousedown on the grip arms the row for dragging, so text selection and chip clicks stay normal.
  const [armedIndex, setArmedIndex] = useState<number | null>(null);

  const setFilters = (filters: string[]) => onChange({ ...value, filters });

  const handleAddFilter = (expression: string) => {
    setFilters([...value.filters, expression]);
  };

  const handleRemoveFilter = (index: number) => {
    setFilters(value.filters.filter((_, i) => i !== index));
    if (editingIndex === index) setEditingIndex(null);
  };

  const handleEditFilter = (index: number, newExpression: string) => {
    setFilters(value.filters.map((f, i) => (i === index ? newExpression : f)));
  };

  const handleSaveEdit = (index: number, newExpression: string) => {
    handleEditFilter(index, newExpression);
    setEditingIndex(null);
  };

  const handleMove = (from: number, to: number) => {
    const next = moveItem(value.filters, from, to);
    if (next !== value.filters) setFilters(next);
  };

  const resetDrag = () => {
    setDragIndex(null);
    setOverIndex(null);
    setArmedIndex(null);
  };

  const handleDragStart = (index: number) => (e: DragEvent<HTMLDivElement>) => {
    if (armedIndex !== index) {
      e.preventDefault();
      return;
    }
    setEditingIndex(null);
    setDragIndex(index);
    e.dataTransfer.effectAllowed = 'move';
    // Firefox requires data to be set for the drag to begin.
    e.dataTransfer.setData('text/plain', String(index));
  };

  const handleDragOver = (index: number) => (e: DragEvent<HTMLDivElement>) => {
    if (dragIndex === null) return;
    e.preventDefault();
    e.dataTransfer.dropEffect = 'move';
    if (overIndex !== index) setOverIndex(index);
  };

  const handleDrop = (index: number) => (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    if (dragIndex !== null) handleMove(dragIndex, index);
    resetDrag();
  };

  /** Keyboard fallback for the grip: arrow keys nudge the row up/down. */
  const handleGripKeyDown = (index: number) => (e: KeyboardEvent<HTMLButtonElement>) => {
    if (e.key === 'ArrowUp') {
      e.preventDefault();
      handleMove(index, index - 1);
    } else if (e.key === 'ArrowDown') {
      e.preventDefault();
      handleMove(index, index + 1);
    }
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
                : `${value.filters.length} filter${value.filters.length !== 1 ? 's' : ''} configured (combined with AND) — click a condition to edit, drag the handle to reorder`}
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
            {value.filters.map((filter, index) => {
              const isEditing = editingIndex === index;
              const isDragging = dragIndex === index;
              const isDropTarget = dragIndex !== null && overIndex === index && dragIndex !== index;
              const dropAbove = isDropTarget && dragIndex !== null && dragIndex > index;

              if (isEditing) {
                return (
                  <div
                    key={`${filter}-${index}-edit`}
                    className="flex items-start gap-2 p-3 rounded-lg bg-card border border-ring/60 ring-2 ring-ring/20"
                  >
                    <span className="mt-2 w-5 text-xs font-mono text-muted-foreground">{index + 1}.</span>
                    <div className="flex-1 min-w-0">
                      <FilterComposer
                        context={context}
                        initialExpression={filter}
                        autoFocus
                        addButtonLabel="Save"
                        onAddFilter={(expression) => handleSaveEdit(index, expression)}
                        onCancel={() => setEditingIndex(null)}
                      />
                    </div>
                  </div>
                );
              }

              return (
                <div
                  key={`${filter}-${index}`}
                  role="button"
                  tabIndex={0}
                  draggable={armedIndex === index}
                  onDragStart={handleDragStart(index)}
                  onDragOver={handleDragOver(index)}
                  onDrop={handleDrop(index)}
                  onDragEnd={resetDrag}
                  onClick={() => setEditingIndex(index)}
                  onKeyDown={(e) => {
                    if (e.target !== e.currentTarget) return;
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault();
                      setEditingIndex(index);
                    }
                  }}
                  title="Click to edit"
                  className={`group flex items-center gap-2 p-3 rounded-lg bg-muted/30 border transition-colors cursor-pointer hover:bg-accent/40 focus:outline-none focus-visible:ring-2 focus-visible:ring-ring ${
                    isDragging ? 'opacity-40 border-dashed border-border' : 'border-border'
                  } ${
                    isDropTarget
                      ? dropAbove
                        ? 'shadow-[0_-2px_0_0_hsl(var(--primary))]'
                        : 'shadow-[0_2px_0_0_hsl(var(--primary))]'
                      : ''
                  }`}
                >
                  {/* Drag Handle / Index */}
                  <div className="flex items-center gap-1 text-muted-foreground">
                    <button
                      type="button"
                      aria-label={`Reorder condition ${index + 1}. Drag, or use arrow keys.`}
                      title="Drag to reorder"
                      onMouseDown={(e) => {
                        e.stopPropagation();
                        setArmedIndex(index);
                      }}
                      onMouseUp={() => setArmedIndex(null)}
                      onClick={(e) => e.stopPropagation()}
                      onKeyDown={handleGripKeyDown(index)}
                      className="flex h-6 w-5 items-center justify-center rounded cursor-grab active:cursor-grabbing opacity-40 group-hover:opacity-80 hover:!opacity-100 hover:bg-accent focus:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:opacity-100 transition-opacity"
                    >
                      <GripVertical className="w-4 h-4" />
                    </button>
                    <span className="text-xs font-mono w-5">{index + 1}.</span>
                  </div>

                  {/* Filter Expression — chips when parseable, plain text fallback otherwise */}
                  <div className="flex-1 min-w-0">
                    <FilterChips
                      expression={filter}
                      onChange={(expression) => handleEditFilter(index, expression)}
                    />
                  </div>

                  {/* Actions */}
                  <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 group-focus-within:opacity-100 transition-opacity">
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      aria-label="Edit condition"
                      title="Edit"
                      onClick={(e) => {
                        e.stopPropagation();
                        setEditingIndex(index);
                      }}
                      className="h-7 w-7 p-0 text-muted-foreground hover:text-foreground"
                    >
                      <Pencil className="w-4 h-4" />
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      aria-label="Remove condition"
                      title="Remove"
                      onClick={(e) => {
                        e.stopPropagation();
                        handleRemoveFilter(index);
                      }}
                      className="h-7 w-7 p-0 text-muted-foreground hover:text-red-500"
                    >
                      <Trash2 className="w-4 h-4" />
                    </Button>
                  </div>
                </div>
              );
            })}
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
        <div className="flex items-baseline gap-2">
          <span className="text-sm font-medium text-foreground">Filter Builder</span>
          <span className="text-xs text-muted-foreground">
            Build expressions with indicators, operators, and timeframes
          </span>
        </div>
        <div className="p-4 rounded-lg border border-border bg-card">
          <FilterComposer
            onAddFilter={handleAddFilter}
            context={context}
            addButtonLabel="Add Entry Condition"
          />
        </div>
      </div>
    </div>
  );
}
