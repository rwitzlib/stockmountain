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
              flex items-center gap-2 py-3 px-3 whitespace-nowrap transition-colors text-sm
              ${isActive 
                ? 'bg-accent text-foreground font-medium' 
                : 'text-muted-foreground hover:bg-accent hover:text-foreground'
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
          <div className="text-sm font-semibold tracking-tight text-foreground">
            StockMountain
          </div>
          <button
            onClick={toggleMobileMenu}
            className="p-2 rounded-lg text-muted-foreground hover:bg-accent hover:text-foreground transition-colors border border-border"
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
                  flex items-center gap-3 py-3 px-4 transition-colors text-sm
                  ${isActive 
                    ? 'bg-accent text-foreground font-medium' 
                    : 'text-muted-foreground hover:bg-accent hover:text-foreground'
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