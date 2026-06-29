import { useState } from 'react';
import { NavLink } from 'react-router-dom';
import { Home, BarChart2, LineChart, Settings, Wrench, CandlestickChart, Search, Menu, X, Bot } from 'lucide-react';

const navItems = [
  { path: '/', icon: Home, label: 'Home' },
  { path: '/chart', icon: CandlestickChart, label: 'Charts' },
  { path: '/scanner', icon: Search, label: 'Scanner' },
  { path: '/backtest', icon: BarChart2, label: 'Backtest' },
  { path: '/optimus', icon: Bot, label: 'Optimus' },
  { path: '/tools', icon: Wrench, label: 'Tools' },
  { path: '/live', icon: LineChart, label: 'Live' },
  { path: '/settings', icon: Settings, label: 'Settings' },
];

export function TopNav() {
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);

  const toggleMobileMenu = () => {
    setIsMobileMenuOpen(!isMobileMenuOpen);
  };

  const closeMobileMenu = () => {
    setIsMobileMenuOpen(false);
  };

  return (
    <nav className="fixed top-0 left-0 right-0 z-50 border-b border-border bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/90">
      {/* Desktop Navigation */}
      <div className="hidden md:flex justify-between px-4 overflow-x-auto">
        {navItems.map(({ path, icon: Icon, label }) => (
          <NavLink
            key={path}
            to={path}
            className={({ isActive }) => `
              flex items-center gap-2 py-3 px-3 border-b-2 whitespace-nowrap transition-all font-mono text-xs uppercase tracking-wider
              ${isActive 
                ? 'border-primary text-primary bg-primary/10 dark:bg-primary/20' 
                : 'border-transparent text-muted-foreground hover:text-primary hover:bg-muted/50'
              }
            `}
          >
            <Icon className="w-4 h-4" />
            <span>{label}</span>
          </NavLink>
        ))}
      </div>

      {/* Mobile Navigation */}
      <div className="md:hidden">
        {/* Mobile Header with Hamburger */}
        <div className="flex justify-between items-center px-4 py-3 border-b border-border">
          <div className="text-sm font-mono font-bold uppercase tracking-wider text-primary">
            StockMountain
          </div>
          <button
            onClick={toggleMobileMenu}
            className="p-2 text-muted-foreground hover:text-primary hover:bg-muted transition-colors border border-border hover:border-primary"
            aria-label="Toggle navigation menu"
          >
            {isMobileMenuOpen ? (
              <X className="w-5 h-5" />
            ) : (
              <Menu className="w-5 h-5" />
            )}
          </button>
        </div>

        {/* Mobile Menu Items */}
        {isMobileMenuOpen && (
          <div className="border-t border-border bg-background">
            {navItems.map(({ path, icon: Icon, label }) => (
              <NavLink
                key={path}
                to={path}
                onClick={closeMobileMenu}
                className={({ isActive }) => `
                  flex items-center gap-3 py-3 px-4 border-l-2 transition-all font-mono text-xs uppercase tracking-wider
                  ${isActive 
                    ? 'border-primary text-primary bg-primary/10 dark:bg-primary/20' 
                    : 'border-transparent text-muted-foreground hover:text-primary hover:bg-muted/50 hover:border-primary'
                  }
                `}
              >
                <Icon className="w-4 h-4" />
                <span>{label}</span>
              </NavLink>
            ))}
          </div>
        )}
      </div>
    </nav>
  );
}