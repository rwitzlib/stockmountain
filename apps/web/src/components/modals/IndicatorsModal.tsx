import { useMemo, useState, useEffect, type DragEvent } from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '../ui/dialog';
import { Button } from '../ui/button';
import { Input } from '../ui/input';
import { X, Plus, TrendingUp, Pencil, ArrowUp, ArrowDown, Trash2 } from 'lucide-react';
import { IndicatorSetup, IndicatorType } from '../../types/tools';
import { INDICATOR_TYPES, getIndicatorDefinition, getDefaultColor } from '../../config/indicators';

interface IndicatorsModalProps {
  isOpen: boolean;
  onClose: () => void;
  onApply: (indicators: IndicatorSetup[]) => void;
  currentIndicators?: IndicatorSetup[];
}

interface IndicatorFormData {
  type: IndicatorType;
  pane: number;
  params: Record<string, string>;
  color: string;
  colors?: Record<string, string>;
}

export function IndicatorsModal({ isOpen, onClose, onApply, currentIndicators = [] }: IndicatorsModalProps) {
  const [indicators, setIndicators] = useState<IndicatorFormData[]>([]);
  const [selectedType, setSelectedType] = useState<IndicatorType>('sma');
  const [editingIndex, setEditingIndex] = useState<number | null>(null);
  const [dragIndex, setDragIndex] = useState<number | null>(null);
  const [dragOverIndex, setDragOverIndex] = useState<number | null>(null);

  const categories = useMemo(() => {
    const map: Record<string, { label: string; types: IndicatorType[] }> = {};
    INDICATOR_TYPES.forEach(t => {
      const def = getIndicatorDefinition(t);
      if (!map[def.category]) {
        map[def.category] = { label: def.category, types: [] };
      }
      map[def.category].types.push(t);
    });
    return map;
  }, []);

  // Initialize indicators from currentIndicators when modal opens
  useEffect(() => {
    if (isOpen && currentIndicators.length > 0) {
      const formData: IndicatorFormData[] = currentIndicators.map((indicator, index) => ({
        type: indicator.type,
        pane: indicator.pane,
        params: Object.fromEntries(
          Object.entries(indicator.params).map(([key, value]) => [key, String(value)])
        ),
        color: indicator.color || getDefaultColor(index),
        colors: indicator.colors
      }));
      setIndicators(formData);
    } else if (isOpen) {
      setIndicators([]);
    }
  }, [isOpen, currentIndicators]);

  const toSetup = (forms: IndicatorFormData[]): IndicatorSetup[] => forms.map(indicator => ({
    type: indicator.type,
    pane: indicator.pane,
    params: Object.fromEntries(
      Object.entries(indicator.params).map(([key, value]) => [key, parseFloat(value) || value])
    ),
    color: indicator.color,
    colors: indicator.colors
  }));

  const applyNow = (next: IndicatorFormData[]) => {
    setIndicators(next);
    onApply(toSetup(next));
  };

  const addIndicator = () => {
    const def = getIndicatorDefinition(selectedType);
    const next: IndicatorFormData = {
      type: def.type,
      pane: def.defaultPane,
      params: Object.fromEntries(def.params.map(p => [p.key, p.default])),
      color: getDefaultColor(indicators.length),
      colors: def.defaultColors ? { ...def.defaultColors } : undefined,
    };
    applyNow([...indicators, next]);
  };

  const removeIndicator = (index: number) => {
    applyNow(indicators.filter((_, i) => i !== index));
  };

  const updateIndicator = (index: number, field: keyof IndicatorFormData, value: any) => {
    const next = indicators.map((indicator, i) => {
      if (i !== index) return indicator;

      if (field === 'type') {
        const newType = value as IndicatorType;
        const def = getIndicatorDefinition(newType);
        const defaultParams = Object.fromEntries(def.params.map(param => [param.key, param.default]));
        return {
          ...indicator,
          type: newType,
          pane: def.defaultPane,
          params: defaultParams,
          colors: def.defaultColors ? { ...def.defaultColors } : undefined,
        };
      }

      if (field === 'pane') {
        return { ...indicator, pane: parseInt(value) || 0 };
      }

      if (field === 'params') {
        return { ...indicator, params: { ...indicator.params, ...value } };
      }

      return { ...indicator, [field]: value } as IndicatorFormData;
    });
    applyNow(next);
  };

  const handleApply = () => {
    onApply(toSetup(indicators));
    onClose();
  };

  // Drag & Drop helpers
  const reorderWithinPane = (pane: number, sourceGlobalIndex: number, target: number | 'end') => {
    const indicesInPane: number[] = [];
    for (let i = 0; i < indicators.length; i++) {
      if (indicators[i].pane === pane) indicesInPane.push(i);
    }
    const sourcePos = indicesInPane.indexOf(sourceGlobalIndex);
    const targetPos = target === 'end' ? indicesInPane.length - 1 : indicesInPane.indexOf(target);
    if (sourcePos === -1 || targetPos === -1 || sourcePos === targetPos) return;

    const paneItems = indicesInPane.map(i => indicators[i]);
    const [moved] = paneItems.splice(sourcePos, 1);
    const insertPos = target === 'end' ? paneItems.length + 1 : targetPos;
    paneItems.splice(insertPos > paneItems.length ? paneItems.length : insertPos, 0, moved);

    const next = indicators.slice();
    for (let k = 0; k < indicesInPane.length; k++) {
      // After removal, paneItems length is one less; we inserted moved back, so lengths match again
      next[indicesInPane[k]] = paneItems[k] ?? next[indicesInPane[k]];
    }
    applyNow(next);
  };

  const handleDragStart = (index: number, e: DragEvent) => {
    setDragIndex(index);
    setDragOverIndex(null);
    e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('text/plain', String(index));
  };

  const handleDragOver = (index: number, e: DragEvent) => {
    if (dragIndex === null) return;
    if (indicators[dragIndex].pane !== indicators[index].pane) return;
    e.preventDefault();
    e.dataTransfer.dropEffect = 'move';
    setDragOverIndex(index);
  };

  const handleDrop = (index: number, e: DragEvent) => {
    e.preventDefault();
    if (dragIndex === null) return;
    const src = dragIndex;
    const dst = index;
    setDragOverIndex(null);
    setDragIndex(null);
    if (src === dst) return;
    const pane = indicators[src].pane;
    if (pane !== indicators[dst].pane) return; // only within same pane
    reorderWithinPane(pane, src, dst);
  };

  const handleDropToPaneEnd = (pane: number, e: DragEvent) => {
    e.preventDefault();
    if (dragIndex === null) return;
    if (indicators[dragIndex].pane !== pane) return;
    reorderWithinPane(pane, dragIndex, 'end');
    setDragOverIndex(null);
    setDragIndex(null);
  };

  const handleDragEnd = () => {
    setDragOverIndex(null);
    setDragIndex(null);
  };

  const renderIndicatorForm = (indicator: IndicatorFormData, index: number) => {
    const config = getIndicatorDefinition(indicator.type);

    return (
      <div className="border border-border rounded-lg p-4 bg-muted/30 dark:bg-gray-900/50">
        <div className="grid grid-cols-3 gap-4">
          {indicator.type !== 'macd' && indicator.type !== 'rsi' && (
            <div>
              <label className="text-xs font-mono text-muted-foreground mb-2 block uppercase tracking-wide">Color</label>
              <div className="flex gap-2">
                <input
                  type="color"
                  value={indicator.color}
                  onChange={(e) => updateIndicator(index, 'color', e.target.value)}
                  className="w-12 h-10 rounded-md border border-border cursor-pointer bg-background dark:bg-gray-800"
                />
                <Input
                  value={indicator.color}
                  onChange={(e) => updateIndicator(index, 'color', e.target.value)}
                  placeholder="#000000"
                  className="flex-1 border border-border bg-background dark:bg-gray-900 text-foreground dark:text-cyan-400 font-mono text-sm focus:border-primary dark:focus:border-cyan-500"
                />
              </div>
            </div>
          )}
        </div>

        <div className="grid grid-cols-2 gap-4">
          {config.params.map(param => (
            <div key={param.key}>
              <label className="text-xs font-mono text-muted-foreground mb-2 block uppercase tracking-wide">{param.label}</label>
              {param.type === 'select' ? (
                <select
                  value={indicator.params[param.key] || param.default}
                  onChange={(e) => updateIndicator(index, 'params', { [param.key]: e.target.value })}
                  className="flex h-10 w-full rounded-md border border-border bg-background dark:bg-gray-900 text-foreground dark:text-cyan-400 font-mono text-sm px-3 py-2 hover:border-primary dark:hover:border-cyan-700 focus:outline-none focus:border-primary dark:focus:border-cyan-500 transition-colors"
                >
                  {param.options?.map(option => (
                    <option key={option} value={option}>{option.toUpperCase()}</option>
                  ))}
                </select>
              ) : (
                <Input
                  type="text"
                  value={indicator.params[param.key] || param.default}
                  onChange={(e) => updateIndicator(index, 'params', { [param.key]: e.target.value })}
                  placeholder={param.default}
                  className="border border-border bg-background dark:bg-gray-900 text-foreground dark:text-cyan-400 font-mono text-sm focus:border-primary dark:focus:border-cyan-500"
                />
              )}
            </div>
          ))}
        </div>

        {/* Extra color controls for multi-line indicators */}
        {indicator.type === 'macd' && (
          <div className="grid grid-cols-3 gap-4">
            {['macd','signal','histogramUp','histogramDown'].map(key => (
              <div key={key}>
                <label className="text-xs font-mono text-muted-foreground mb-2 block uppercase tracking-wide">{key.charAt(0).toUpperCase() + key.slice(1)} Color</label>
                <div className="flex gap-2">
                  <input
                    type="color"
                    value={indicator.colors?.[key] || ''}
                    onChange={(e) => updateIndicator(index, 'colors', { ...(indicator.colors || {}), [key]: e.target.value })}
                    className="w-12 h-10 rounded-md border border-border cursor-pointer bg-background dark:bg-gray-800"
                  />
                  <Input
                    value={indicator.colors?.[key] || ''}
                    onChange={(e) => updateIndicator(index, 'colors', { ...(indicator.colors || {}), [key]: e.target.value })}
                    placeholder="#000000"
                    className="flex-1 border border-border bg-background dark:bg-gray-900 text-foreground dark:text-cyan-400 font-mono text-sm focus:border-primary dark:focus:border-cyan-500"
                  />
                </div>
              </div>
            ))}
          </div>
        )}

        {indicator.type === 'rsi' && (
          <div className="grid grid-cols-3 gap-4">
            {['rsi','upper','lower'].map(key => (
              <div key={key}>
                <label className="text-xs font-mono text-muted-foreground mb-2 block uppercase tracking-wide">{key.charAt(0).toUpperCase() + key.slice(1)} Color</label>
                <div className="flex gap-2">
                  <input
                    type="color"
                    value={indicator.colors?.[key] || ''}
                    onChange={(e) => updateIndicator(index, 'colors', { ...(indicator.colors || {}), [key]: e.target.value })}
                    className="w-12 h-10 rounded-md border border-border cursor-pointer bg-background dark:bg-gray-800"
                  />
                  <Input
                    value={indicator.colors?.[key] || ''}
                    onChange={(e) => updateIndicator(index, 'colors', { ...(indicator.colors || {}), [key]: e.target.value })}
                    placeholder="#000000"
                    className="flex-1 border border-border bg-background dark:bg-gray-900 text-foreground dark:text-cyan-400 font-mono text-sm focus:border-primary dark:focus:border-cyan-500"
                  />
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    );
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-[950px] max-h-[90vh] overflow-y-auto bg-popover border-border">
        <DialogHeader className="border-b border-border pb-4">
          <DialogTitle className="text-xl font-mono font-semibold text-primary uppercase tracking-wider">
            :: INDICATORS CONFIG ::
          </DialogTitle>
        </DialogHeader>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-6 pt-4">
          <div className="md:col-span-1 space-y-4">
            <div className="border border-border rounded-lg p-4 space-y-3 bg-card">
              <div className="font-mono font-semibold text-primary uppercase tracking-wide text-sm">{'>'} Add Indicator</div>
              <div>
                <label className="text-xs font-mono text-muted-foreground mb-2 block uppercase tracking-wide">Indicator</label>
                <select
                  value={selectedType}
                  onChange={(e) => setSelectedType(e.target.value as IndicatorType)}
                  className="flex h-10 w-full rounded-md border border-border bg-background text-foreground dark:text-cyan-400 font-mono text-sm px-3 py-2 hover:border-primary dark:hover:border-cyan-700 focus:outline-none focus:border-primary dark:focus:border-cyan-500 transition-colors"
                >
                  {Object.values(categories).map(group => (
                    <optgroup key={group.label} label={group.label.toUpperCase()}>
                      {group.types.map(t => (
                        <option key={t} value={t}>{getIndicatorDefinition(t).name}</option>
                      ))}
                    </optgroup>
                  ))}
                </select>
              </div>
              <Button onClick={addIndicator} className="w-full bg-background dark:bg-gray-800 border border-border dark:border-gray-700 text-primary dark:text-cyan-400 hover:bg-muted dark:hover:bg-gray-700 hover:border-primary dark:hover:border-cyan-500 font-mono uppercase text-sm tracking-wide transition-colors">
                <Plus className="w-4 h-4 mr-2" />
                Add
              </Button>
            </div>

            <div className="border border-border rounded-lg p-4 bg-card">
              <div className="text-xs text-muted-foreground font-mono leading-relaxed">
                {'>'} Select an indicator and click Add. Changes on the right apply immediately.
              </div>
            </div>
          </div>

          <div className="md:col-span-2 space-y-4">
            {(() => {
              // Group indicators by pane and render with horizontal separators
              const byPane = new Map<number, { index: number; item: IndicatorFormData }[]>();
              indicators.forEach((it, idx) => {
                const list = byPane.get(it.pane) || [];
                list.push({ index: idx, item: it });
                byPane.set(it.pane, list);
              });
              const panes = Array.from(byPane.keys()).sort((a, b) => a - b);

              if (panes.length === 0) {
                return (
                  <div className="text-center py-8 text-muted-foreground font-mono text-sm">
                    :: NO INDICATORS CONFIGURED ::
                    <br />
                    <span className="text-xs">Use the left panel to add indicators</span>
                  </div>
                );
              }

              return panes.map(pane => (
                <div key={pane} className="space-y-3">
                  <div className="flex items-center gap-2">
                    <div className="flex-1 h-px bg-border" />
                    <div className="text-xs text-primary dark:text-cyan-400 font-mono uppercase tracking-wide px-2">Pane {pane}</div>
                    <div className="flex-1 h-px bg-border" />
                  </div>
                  <div className="space-y-3">
                    {byPane.get(pane)!.map(({ index, item }) => {
                      const def = getIndicatorDefinition(item.type);
                      const summary = `${def.type.toUpperCase()}(${def.params
                        .map(p => item.params[p.key] ?? p.default)
                        .join(',')})`;
                      const isEditing = editingIndex === index;
                      return (
                        <div
                          key={index}
                          className={`border border-border rounded-lg p-3 bg-card/50 dark:bg-gray-900/30 ${dragOverIndex === index ? 'ring-2 ring-primary dark:ring-cyan-500' : ''} hover:border-primary/50 dark:hover:border-gray-600 transition-colors`}
                          draggable
                          onDragStart={(e) => handleDragStart(index, e)}
                          onDragOver={(e) => handleDragOver(index, e)}
                          onDrop={(e) => handleDrop(index, e)}
                          onDragEnd={handleDragEnd}
                        >
                          <div className="flex items-center justify-between gap-3">
                            <div className="flex items-center gap-3 min-w-0">
                              <div className="w-4 h-4 rounded-sm border border-border" style={{ backgroundColor: item.color }} />
                              <div className="truncate">
                                <div className="font-mono font-medium text-primary dark:text-cyan-400 truncate text-sm">{def.name}</div>
                                <div className="text-xs text-muted-foreground font-mono truncate">{summary}</div>
                              </div>
                            </div>
                            <div className="flex items-center gap-1 flex-shrink-0">
                              <Button variant="ghost" size="sm" onClick={() => setEditingIndex(isEditing ? null : index)} title="Edit" className="text-muted-foreground hover:text-primary dark:hover:text-cyan-400 hover:bg-muted dark:hover:bg-gray-800">
                                <Pencil className="w-4 h-4" />
                              </Button>
                              <Button variant="ghost" size="sm" onClick={() => updateIndicator(index, 'pane', String(Math.max(0, item.pane - 1)))} title="Move to previous pane" className="text-muted-foreground hover:text-primary dark:hover:text-cyan-400 hover:bg-muted dark:hover:bg-gray-800">
                                <ArrowUp className="w-4 h-4" />
                              </Button>
                              <Button variant="ghost" size="sm" onClick={() => updateIndicator(index, 'pane', String(item.pane + 1))} title="Move to next pane" className="text-muted-foreground hover:text-primary dark:hover:text-cyan-400 hover:bg-muted dark:hover:bg-gray-800">
                                <ArrowDown className="w-4 h-4" />
                              </Button>
                              <Button variant="ghost" size="sm" onClick={() => removeIndicator(index)} title="Delete" className="text-muted-foreground hover:text-red-600 dark:hover:text-red-400 hover:bg-red-100/50 dark:hover:bg-gray-800">
                                <Trash2 className="w-4 h-4" />
                              </Button>
                            </div>
                          </div>
                          {isEditing && (
                            <div className="mt-3">
                              {renderIndicatorForm(item, index)}
                            </div>
                          )}
                        </div>
                      );
                    })}
                    {/* Drop zone at end of pane */}
                    <div
                      className="h-8 rounded-md border border-dashed border-border flex items-center justify-center text-xs text-muted-foreground font-mono hover:border-primary dark:hover:border-cyan-500 hover:text-primary dark:hover:text-cyan-400 transition-colors"
                      onDragOver={(e) => {
                        if (dragIndex !== null && indicators[dragIndex].pane === pane) {
                          e.preventDefault();
                        }
                      }}
                      onDrop={(e) => handleDropToPaneEnd(pane, e)}
                    >
                      :: DROP TO END OF PANE {pane} ::
                    </div>
                  </div>
                </div>
              ));
            })()}
          </div>
        </div>

        <DialogFooter className="border-t border-border pt-4">
          <Button variant="outline" onClick={onClose} className="border-border text-muted-foreground hover:bg-muted dark:hover:bg-gray-800 hover:border-border font-mono uppercase text-sm tracking-wide">
            Close
          </Button>
          <Button onClick={handleApply} className="bg-primary/10 dark:bg-gray-800 border border-primary dark:border-gray-600 text-primary dark:text-cyan-400 hover:bg-primary/20 dark:hover:bg-gray-700 hover:border-primary dark:hover:border-cyan-500 font-mono uppercase text-sm tracking-wide">
            Apply Changes
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
