import { useEffect, useState } from 'react';
import { UserButton, useUser } from '@clerk/react';
import { NavLink, useLocation } from 'react-router-dom';
import {
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  CreditCard,
  Menu,
  Settings,
  User,
  X,
} from 'lucide-react';
import { ThemeToggle } from './ThemeToggle';
import { Brand, BrandMark } from './Brand';
import { navItems, NavChild } from './navConfig';
import { useIsAdmin } from '../../hooks/useIsAdmin';

const COLLAPSED_STORAGE_KEY = 'sm-sidebar-collapsed';

const accountItems: NavChild[] = [
  { path: '/profile', icon: User, label: 'Profile' },
  { path: '/billing', icon: CreditCard, label: 'Billing' },
  { path: '/settings', icon: Settings, label: 'Settings' },
];

function navLinkClasses(isActive: boolean, indented = false) {
  return `
    flex items-center gap-3 rounded-lg px-3 py-2 mb-0.5 text-sm transition-colors
    ${indented ? 'ml-6' : ''}
    ${isActive
      ? 'bg-accent font-medium text-foreground'
      : 'text-muted-foreground hover:bg-accent/60 hover:text-foreground'}
  `;
}

interface ClerkAuthControlsProps {
  isCollapsed?: boolean;
  onAction?: () => void;
}

function ClerkAuthControls({ isCollapsed = false, onAction }: ClerkAuthControlsProps) {
  const { isLoaded, isSignedIn } = useUser();

  if (!isLoaded) {
    return (
      <div className="text-xs text-muted-foreground animate-pulse">
        {!isCollapsed ? 'Loading…' : '…'}
      </div>
    );
  }

  if (isSignedIn) {
    return <UserButton afterSignOutUrl="/" />;
  }

  return (
    <div className="flex items-center gap-2">
      <NavLink
        to="/sign-in"
        className="inline-flex items-center gap-2 rounded-lg border border-border px-3 py-2 text-xs font-medium text-muted-foreground transition-colors hover:bg-accent hover:text-foreground"
        onClick={onAction}
      >
        {isCollapsed ? <User className="w-4 h-4" /> : 'Sign in'}
      </NavLink>
      {!isCollapsed && (
        <NavLink
          to="/sign-up"
          className="inline-flex items-center gap-2 rounded-lg bg-primary px-3 py-2 text-xs font-medium text-primary-foreground transition-colors hover:bg-primary/90"
          onClick={onAction}
        >
          Sign up
        </NavLink>
      )}
    </div>
  );
}

interface NavListProps {
  isCollapsed?: boolean;
  onNavigate?: () => void;
}

/** The main nav tree, shared between desktop sidebar and mobile menu. */
function NavList({ isCollapsed = false, onNavigate }: NavListProps) {
  const location = useLocation();
  const { isAdmin } = useIsAdmin();
  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>({});

  // Auto-expand the group containing the current route.
  useEffect(() => {
    for (const item of navItems) {
      if (item.children && location.pathname.startsWith(item.path)) {
        setOpenGroups((prev) => (prev[item.path] ? prev : { ...prev, [item.path]: true }));
      }
    }
  }, [location.pathname]);

  return (
    <>
      {navItems.map(({ path, icon: Icon, label, end, children: allChildren }) => {
        const children = allChildren?.filter((child) => !child.adminOnly || isAdmin);
        const isGroupOpen = !!openGroups[path];
        const showChildren = !!children && !isCollapsed && isGroupOpen;

        return (
          <div key={path}>
            <div className="flex items-center">
              <NavLink
                to={path}
                end={end}
                onClick={onNavigate}
                className={({ isActive }) =>
                  // A group parent only highlights via its children (Overview
                  // covers the exact path), except when collapsed.
                  navLinkClasses(isActive && (!children || isCollapsed)) + ' flex-1'
                }
              >
                <Icon className="w-4 h-4 shrink-0" />
                {!isCollapsed && <span>{label}</span>}
              </NavLink>
              {children && !isCollapsed && (
                <button
                  onClick={() =>
                    setOpenGroups((prev) => ({ ...prev, [path]: !prev[path] }))
                  }
                  className="rounded-lg p-1.5 mb-0.5 text-muted-foreground transition-colors hover:bg-accent/60 hover:text-foreground"
                  aria-label={`${isGroupOpen ? 'Collapse' : 'Expand'} ${label} section`}
                  aria-expanded={isGroupOpen}
                >
                  <ChevronDown
                    className={`w-3.5 h-3.5 transition-transform ${isGroupOpen ? '' : '-rotate-90'}`}
                  />
                </button>
              )}
            </div>
            {showChildren &&
              children.map(({ path: childPath, icon: ChildIcon, label: childLabel, end: childEnd }) => (
                <NavLink
                  key={childPath}
                  to={childPath}
                  end={childEnd}
                  onClick={onNavigate}
                  className={({ isActive }) => navLinkClasses(isActive, true)}
                >
                  <ChildIcon className="w-4 h-4 shrink-0" />
                  <span>{childLabel}</span>
                </NavLink>
              ))}
          </div>
        );
      })}
    </>
  );
}

interface AccountSectionProps {
  isCollapsed?: boolean;
  onNavigate?: () => void;
}

/** Signed-in account footer: avatar + name row that expands Profile/Billing/Settings/theme. */
function AccountSection({ isCollapsed = false, onNavigate }: AccountSectionProps) {
  const { isLoaded, isSignedIn, user } = useUser();
  const [isOpen, setIsOpen] = useState(false);

  const displayName =
    user?.fullName ?? user?.username ?? user?.primaryEmailAddress?.emailAddress ?? 'Account';

  if (!isLoaded) {
    return <div className="px-3 py-2 text-xs text-muted-foreground animate-pulse">Loading…</div>;
  }

  if (!isSignedIn) {
    return (
      <div className="px-3 py-2 space-y-2">
        <ClerkAuthControls isCollapsed={isCollapsed} onAction={onNavigate} />
        <ThemeToggle isCollapsed={isCollapsed} />
      </div>
    );
  }

  if (isCollapsed) {
    return (
      <div className="flex flex-col items-center gap-2 py-2">
        <UserButton afterSignOutUrl="/" />
        <ThemeToggle isCollapsed />
      </div>
    );
  }

  return (
    <div className="px-2 py-2">
      {isOpen && (
        <div className="mb-1 border-b border-sidebar-border pb-1">
          {accountItems.map(({ path, icon: Icon, label }) => (
            <NavLink
              key={path}
              to={path}
              onClick={onNavigate}
              className={({ isActive }) => navLinkClasses(isActive)}
            >
              <Icon className="w-4 h-4 shrink-0" />
              <span>{label}</span>
            </NavLink>
          ))}
          <ThemeToggle />
        </div>
      )}
      <div className="flex items-center gap-2 rounded-lg px-1 py-1">
        <UserButton afterSignOutUrl="/" />
        <button
          onClick={() => setIsOpen(!isOpen)}
          className="flex flex-1 items-center justify-between gap-2 rounded-lg px-2 py-1.5 text-sm text-muted-foreground transition-colors hover:bg-accent/60 hover:text-foreground"
          aria-expanded={isOpen}
        >
          <span className="truncate text-left text-xs">{displayName}</span>
          <ChevronDown
            className={`w-3.5 h-3.5 shrink-0 transition-transform ${isOpen ? '' : 'rotate-180'}`}
          />
        </button>
      </div>
    </div>
  );
}

export function Sidebar() {
  const [isCollapsed, setIsCollapsed] = useState(() => {
    try {
      return localStorage.getItem(COLLAPSED_STORAGE_KEY) === '1';
    } catch {
      return false;
    }
  });
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);

  const toggleCollapsed = () => {
    setIsCollapsed((prev) => {
      const next = !prev;
      try {
        localStorage.setItem(COLLAPSED_STORAGE_KEY, next ? '1' : '0');
      } catch {
        // Persistence is best-effort (e.g. private browsing)
      }
      return next;
    });
  };

  const closeMobileMenu = () => setIsMobileMenuOpen(false);

  return (
    <>
      {/* Mobile Navigation */}
      <div className="md:hidden">
        <nav className="fixed top-0 left-0 right-0 z-50 border-b border-border bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/90">
          <div className="flex justify-between items-center px-4 py-3">
            <NavLink to="/" onClick={closeMobileMenu}>
              <Brand />
            </NavLink>
            <div className="flex items-center gap-2">
              <ClerkAuthControls onAction={closeMobileMenu} />
              <button
                onClick={() => setIsMobileMenuOpen(!isMobileMenuOpen)}
                className="rounded-lg p-2 text-muted-foreground transition-colors hover:bg-accent hover:text-foreground"
                aria-label="Toggle navigation menu"
              >
                {isMobileMenuOpen ? <X className="w-5 h-5" /> : <Menu className="w-5 h-5" />}
              </button>
            </div>
          </div>

          {isMobileMenuOpen && (
            <div className="border-t border-border bg-background px-2 py-2">
              <NavList onNavigate={closeMobileMenu} />
              <div className="mt-1 border-t border-border pt-1">
                {accountItems.map(({ path, icon: Icon, label }) => (
                  <NavLink
                    key={path}
                    to={path}
                    onClick={closeMobileMenu}
                    className={({ isActive }) => navLinkClasses(isActive)}
                  >
                    <Icon className="w-4 h-4 shrink-0" />
                    <span>{label}</span>
                  </NavLink>
                ))}
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
        className={`hidden md:flex md:flex-col bg-sidebar text-sidebar-foreground border-r border-sidebar-border h-screen sticky top-0 transition-all duration-300 ${
          isCollapsed ? 'w-16' : 'w-64'
        }`}
      >
        <div
          className={`flex items-center border-b border-sidebar-border p-3 ${
            isCollapsed ? 'flex-col gap-2' : 'justify-between'
          }`}
        >
          <NavLink to="/" aria-label="StockMountain dashboard">
            {isCollapsed ? <BrandMark className="h-7 w-7 rounded-lg ring-1 ring-border" /> : <Brand />}
          </NavLink>
          <button
            onClick={toggleCollapsed}
            className="rounded-lg p-1.5 text-muted-foreground transition-colors hover:bg-accent hover:text-foreground"
            aria-label={isCollapsed ? 'Expand sidebar' : 'Collapse sidebar'}
          >
            {isCollapsed ? <ChevronRight className="w-4 h-4" /> : <ChevronLeft className="w-4 h-4" />}
          </button>
        </div>

        <nav className="px-2 py-4 flex-1 overflow-y-auto">
          <NavList isCollapsed={isCollapsed} />
        </nav>

        <div className="border-t border-sidebar-border">
          <AccountSection isCollapsed={isCollapsed} />
        </div>
      </div>
    </>
  );
}
