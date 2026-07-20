import { useEffect, useRef, useState } from 'react';

interface ContextMenuProps {
  children: React.ReactNode;
  onDelete: () => void;
}

export function ContextMenu({ children, onDelete }: ContextMenuProps) {
  const [showMenu, setShowMenu] = useState(false);
  const [position, setPosition] = useState({ x: 0, y: 0 });
  const menuRef = useRef<HTMLDivElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(event.target as Node)) {
        setShowMenu(false);
      }
    };

    document.addEventListener('click', handleClickOutside);
    return () => document.removeEventListener('click', handleClickOutside);
  }, []);

  const handleContextMenu = (e: React.MouseEvent) => {
    e.preventDefault();
    
    if (!containerRef.current) return;

    const rect = containerRef.current.getBoundingClientRect();
    const menuWidth = 192; // w-48 = 12rem = 192px
    const menuHeight = 36; // Approximate height of menu
    const windowWidth = window.innerWidth;
    const windowHeight = window.innerHeight;

    // Calculate position relative to the container
    const x = Math.min(e.clientX - rect.left, windowWidth - menuWidth);
    const y = Math.min(e.clientY - rect.top, windowHeight - menuHeight);

    setPosition({ x, y });
    setShowMenu(true);
  };

  return (
    <div ref={containerRef} onContextMenu={handleContextMenu} className="relative">
      {children}
      {showMenu && (
        <div
          ref={menuRef}
          className="absolute z-50 rounded-lg bg-popover border border-border shadow-lg py-1 w-48"
          style={{
            left: `${position.x}px`,
            top: `${position.y}px`,
          }}
        >
          <button
            onClick={() => {
              onDelete();
              setShowMenu(false);
            }}
            className="w-full px-4 py-2 text-left text-xs text-red-600 dark:text-red-400 hover:bg-accent transition-colors"
          >
            Delete Chart
          </button>
        </div>
      )}
    </div>
  );
}