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
        className="flex items-center gap-2 px-3 py-1.5 bg-background dark:bg-gray-900 text-primary dark:text-cyan-400 text-xs font-mono uppercase border border-border dark:border-gray-800 hover:border-primary dark:hover:border-cyan-700 hover:bg-muted dark:hover:bg-gray-800 transition-all shadow-sm hover:shadow-primary/20 dark:hover:shadow-cyan-900/50"
      >
        <LayoutDashboard className="w-3 h-3" />
        Widgets
      </button>

      {isOpen && (
        <div className="absolute right-0 mt-1 w-52 bg-popover dark:bg-gray-900 border border-border dark:border-gray-700 shadow-xl shadow-black/20 dark:shadow-black/50 py-1 z-50">
          <button
            onClick={() => {
              onAddChart();
              setIsOpen(false);
            }}
            className="flex items-center gap-2 w-full px-4 py-2 text-xs font-mono uppercase text-green-600 dark:text-green-400 hover:bg-muted dark:hover:bg-gray-800 hover:text-green-700 dark:hover:text-green-300 border-l-2 border-transparent hover:border-green-500 dark:hover:border-green-700 transition-all"
          >
            <Plus className="w-3 h-3" />
            Add Chart
          </button>
          <div className="border-t border-border dark:border-gray-800 my-1" />
          <button
            onClick={() => {
              onRemoveAll();
              setIsOpen(false);
            }}
            className="flex items-center gap-2 w-full px-4 py-2 text-xs font-mono uppercase text-red-600 dark:text-red-400 hover:bg-red-100/50 dark:hover:bg-red-950/30 hover:text-red-700 dark:hover:text-red-300 border-l-2 border-transparent hover:border-red-500 dark:hover:border-red-700 transition-all"
          >
            <Trash2 className="w-3 h-3" />
            Remove All
          </button>
        </div>
      )}
    </div>
  );
}