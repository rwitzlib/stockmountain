import { useState } from 'react';
import { UserButton, useUser } from '@clerk/react';
import { NavLink } from 'react-router-dom';
import {
  Home,
  BarChart2,
  LineChart,
  Settings,
  ChevronLeft,
  ChevronRight,
  Wrench,
  CandlestickChart,
  Search,
  User,
  Bot,
  Menu,
  X
} from 'lucide-react';
import { ThemeToggle } from './ThemeToggle';

const navItems = [
  { path: '/', icon: Home, label: 'Home' },
  { path: '/chart', icon: CandlestickChart, label: 'Stock Charts' },
  { path: '/scanner', icon: Search, label: 'Scanner' },
  { path: '/backtest', icon: BarChart2, label: 'Backtest' },
  { path: '/optimus', icon: Bot, label: 'Optimus' },
  { path: '/tools', icon: Wrench, label: 'Tools' },
  { path: '/live', icon: LineChart, label: 'Live Trading' },
  { path: '/settings', icon: Settings, label: 'Settings' },
];

interface ClerkAuthControlsProps {
  isCollapsed?: boolean;
  onAction?: () => void;
}

function ClerkAuthControls({ isCollapsed = false, onAction }: ClerkAuthControlsProps) {
  const { isLoaded, isSignedIn } = useUser();

  if (!isLoaded) {
    return (
      <div className="text-primary font-mono text-xs animate-pulse">
        {!isCollapsed ? '» Clerk...' : '»'}
      </div>
    );
  }

  return (
    <div className="flex items-center gap-2">
      {isSignedIn ? (
        <div className="flex items-center gap-2">
          <UserButton afterSignOutUrl="/" />
          {!isCollapsed && (
            <span className="text-xs font-mono uppercase tracking-wider text-muted-foreground">
              Account
            </span>
          )}
        </div>
      ) : (
        <>
          <NavLink
            to="/sign-in"
            className="inline-flex items-center gap-2 border border-border px-3 py-2 text-xs font-mono uppercase tracking-wider text-muted-foreground transition-all hover:border-primary hover:bg-muted/50 hover:text-primary dark:hover:border-cyan-700 dark:hover:bg-cyan-950/30 dark:hover:text-cyan-400"
            onClick={onAction}
          >
            {isCollapsed ? <User className="w-4 h-4" /> : 'Sign in'}
          </NavLink>
          {!isCollapsed && (
            <NavLink
              to="/sign-up"
              className="inline-flex items-center gap-2 border border-primary bg-primary px-3 py-2 text-xs font-mono uppercase tracking-wider text-primary-foreground transition-all hover:bg-primary/90"
              onClick={onAction}
            >
              Sign up
            </NavLink>
          )}
        </>
      )}
    </div>
  );
}

export function Sidebar() {
  const [isCollapsed, setIsCollapsed] = useState(false);
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const { isLoaded, isSignedIn, user } = useUser();

  const displayName = user?.primaryEmailAddress?.emailAddress ?? user?.username ?? undefined;

  const toggleMobileMenu = () => {
    setIsMobileMenuOpen(!isMobileMenuOpen);
  };

  const closeMobileMenu = () => {
    setIsMobileMenuOpen(false);
  };

  return (
    <>
      {/* Mobile Navigation */}
      <div className="md:hidden">
        {/* Mobile Header with Hamburger */}
        <nav className="fixed top-0 left-0 right-0 z-50 border-b border-border bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/90">
          <div className="flex justify-between items-center px-4 py-3">
            <div className="text-sm font-mono font-bold uppercase tracking-wider text-primary">
              StockMountain
            </div>
            <div className="flex items-center gap-2">
              <ClerkAuthControls onAction={closeMobileMenu} />
              <button
                onClick={toggleMobileMenu}
                className="p-2 text-muted-foreground hover:text-primary dark:hover:text-cyan-400 hover:bg-muted dark:hover:bg-cyan-950/30 transition-colors border border-border hover:border-primary dark:hover:border-cyan-700"
                aria-label="Toggle navigation menu"
              >
                {isMobileMenuOpen ? (
                  <X className="w-5 h-5" />
                ) : (
                  <Menu className="w-5 h-5" />
                )}
              </button>
            </div>
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
                      ? 'border-primary text-primary dark:text-cyan-400 bg-primary/10 dark:bg-primary/20'
                      : 'border-transparent text-muted-foreground hover:text-primary dark:hover:text-cyan-400 hover:bg-muted/50 dark:hover:bg-cyan-950/30 hover:border-primary dark:hover:border-cyan-700'
                    }
                  `}
                >
                  <Icon className="w-4 h-4" />
                  <span>{label}</span>
                </NavLink>
              ))}

              {/* Mobile User Section */}
              <div className="border-t border-border py-2">
                {!isLoaded ? (
                  <div className="px-4 py-3 text-primary font-mono text-xs animate-pulse">» Loading...</div>
                ) : isSignedIn ? (
                  <NavLink
                    to="/profile"
                    onClick={closeMobileMenu}
                    className={({ isActive }) => `
                      flex items-center gap-3 py-3 px-4 border-l-2 transition-all font-mono text-xs uppercase tracking-wider
                      ${isActive
                        ? 'border-primary text-primary bg-primary/10 dark:bg-primary/20'
                        : 'border-transparent text-muted-foreground hover:text-primary hover:bg-muted/50 hover:border-primary'
                      }
                    `}
                  >
                    <User className="w-4 h-4" />
                    <span>{displayName ?? 'Profile'}</span>
                  </NavLink>
                ) : null}
              </div>

              {/* Mobile Theme Toggle */}
              <div className="border-t border-border">
                <ThemeToggle />
              </div>
            </div>
          )}
        </nav>

        {/* Add padding to prevent content from hiding behind fixed nav */}
        <div className="pt-16"></div>
      </div>

      {/* Desktop Sidebar */}
      <div
        className={`hidden md:block bg-sidebar text-sidebar-foreground border-r border-sidebar-border h-screen sticky top-0 transition-all duration-300 flex flex-col ${
          isCollapsed ? 'w-16' : 'w-64'
        }`}
      >
        <div className="p-4 flex justify-end gap-2 border-b border-sidebar-border">
          <button
            onClick={() => setIsCollapsed(!isCollapsed)}
            className="p-2 transition-colors text-muted-foreground hover:text-primary dark:hover:text-cyan-400 hover:bg-muted dark:hover:bg-cyan-950/30 border border-border hover:border-primary dark:hover:border-cyan-700"
          >
            {isCollapsed ? (
              <ChevronRight className="w-4 h-4" />
            ) : (
              <ChevronLeft className="w-4 h-4" />
            )}
          </button>
        </div>

        <nav className="px-2 py-4 flex-1">
          {navItems.map(({ path, icon: Icon, label }) => (
            <NavLink
              key={path}
              to={path}
              className={({ isActive }) => `
                flex items-center gap-3 px-3 py-2.5 mb-1 transition-all border-l-2 font-mono text-xs uppercase tracking-wider
                ${isActive
                  ? 'border-primary bg-primary/10 dark:bg-primary/20 text-primary dark:text-cyan-400'
                  : 'border-transparent text-muted-foreground hover:text-primary dark:hover:text-cyan-400 hover:bg-muted/50 dark:hover:bg-cyan-950/30 hover:border-primary dark:hover:border-cyan-700'}
              `}
            >
              <Icon className="w-4 h-4" />
              {!isCollapsed && <span>{label}</span>}
            </NavLink>
          ))}
        </nav>

        <div className="border-t border-sidebar-border p-4 space-y-2">
          <ClerkAuthControls isCollapsed={isCollapsed} />

          {!isLoaded ? (
            <div className="text-primary font-mono text-xs animate-pulse">» Loading...</div>
          ) : isSignedIn ? (
            <NavLink
              to="/profile"
              className={({ isActive }) => `
                flex items-center gap-3 px-3 py-2.5 transition-all border-l-2 font-mono text-xs uppercase tracking-wider
                ${isActive
                  ? 'border-primary bg-primary/10 dark:bg-primary/20 text-primary dark:text-cyan-400'
                  : 'border-transparent text-muted-foreground hover:text-primary dark:hover:text-cyan-400 hover:bg-muted/50 dark:hover:bg-cyan-950/30 hover:border-primary dark:hover:border-cyan-700'}
              `}
            >
              <User className="w-4 h-4" />
              {!isCollapsed && <span className="truncate">{displayName ?? 'Profile'}</span>}
            </NavLink>
          ) : null}

          {/* Desktop Theme Toggle */}
          <div className="pt-2 border-t border-sidebar-border">
            <ThemeToggle isCollapsed={isCollapsed} />
          </div>
        </div>
      </div>
    </>
  );
}
