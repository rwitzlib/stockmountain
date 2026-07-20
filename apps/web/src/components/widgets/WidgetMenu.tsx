import { useState, useRef, useEffect } from 'react';
import { LayoutDashboard, Plus, Trash2 } from 'lucide-react';

interface WidgetMenuProps {
  onAddChart: () => void;
  onRemoveAll: () => void;
}

export function WidgetMenu({ onAddChart, onRemoveAll }: WidgetMenuProps) {
  const [isOpen, setIsOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  return (
    <div ref={menuRef} className="relative">
      <button
        onClick={() => setIsOpen(!isOpen)}
        className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-card text-foreground text-xs border border-border hover:bg-accent transition-colors"
      >
        <LayoutDashboard className="w-3 h-3" />
        Widgets
      </button>

      {isOpen && (
        <div className="absolute right-0 mt-1 w-52 rounded-lg bg-popover border border-border shadow-lg py-1 z-50">
          <button
            onClick={() => {
              onAddChart();
              setIsOpen(false);
            }}
            className="flex items-center gap-2 w-full px-4 py-2 text-xs text-foreground hover:bg-accent transition-colors"
          >
            <Plus className="w-3 h-3" />
            Add Chart
          </button>
          <div className="border-t border-border my-1" />
          <button
            onClick={() => {
              onRemoveAll();
              setIsOpen(false);
            }}
            className="flex items-center gap-2 w-full px-4 py-2 text-xs text-red-600 dark:text-red-400 hover:bg-accent transition-colors"
          >
            <Trash2 className="w-3 h-3" />
            Remove All
          </button>
        </div>
      )}
    </div>
  );
}