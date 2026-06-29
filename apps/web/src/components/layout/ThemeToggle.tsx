import { useState, useEffect } from 'react';
import { Sun, Moon } from 'lucide-react';

interface ThemeToggleProps {
  isCollapsed?: boolean;
}

export function ThemeToggle({ isCollapsed = false }: ThemeToggleProps) {
  const [isDark, setIsDark] = useState(true);

  useEffect(() => {
    // Check if dark class is present on document element
    const isDarkMode = document.documentElement.classList.contains('dark');
    setIsDark(isDarkMode);
    
    // Also check localStorage for saved theme preference
    const savedTheme = localStorage.getItem('theme');
    if (savedTheme === 'light' && isDarkMode) {
      document.documentElement.classList.remove('dark');
      setIsDark(false);
    } else if (savedTheme === 'dark' && !isDarkMode) {
      document.documentElement.classList.add('dark');
      setIsDark(true);
    }
  }, []);

  const toggleTheme = () => {
    const newIsDark = !isDark;
    setIsDark(newIsDark);
    
    if (newIsDark) {
      document.documentElement.classList.add('dark');
      localStorage.setItem('theme', 'dark');
    } else {
      document.documentElement.classList.remove('dark');
      localStorage.setItem('theme', 'light');
    }
  };

  return (
    <button
      onClick={toggleTheme}
      className="w-full flex items-center gap-3 px-3 py-2.5 border-l-2 border-transparent text-muted-foreground hover:text-primary hover:bg-muted/50 hover:border-primary font-mono text-xs uppercase tracking-wider transition-all"
      aria-label="Toggle theme"
    >
      {isDark ? (
        <Moon className="w-4 h-4" />
      ) : (
        <Sun className="w-4 h-4" />
      )}
      {!isCollapsed && <span className="truncate">{isDark ? 'Dark' : 'Light'}</span>}
    </button>
  );
}

