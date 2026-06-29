import { useRef, useState, useEffect } from 'react';
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
  X,
  LogOut
} from 'lucide-react';
import { createClient } from "@openauthjs/openauth/client"
import { jwtDecode } from 'jwt-decode';
import { userApi } from '../../api/userApi';
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

interface JWTPayload {
  sub: string;
  properties: {
    id: number;
    username: string;
    email: string | null;
    avatar: string | null;
  }
}

const client = createClient({
  clientID: "react",
  issuer: "https://auth.stockmountain.io"
})

export function Sidebar() {
  const [isCollapsed, setIsCollapsed] = useState(false);
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const initializing = useRef(true)
  const accessToken = useRef<string | undefined>(undefined)
  const [isAuthenticating, setIsAuthenticating] = useState(true)
  const [username, setUsername] = useState<string | undefined>()
  const [avatar, setAvatar] = useState<string | undefined>()
  const [hasToken, setHasToken] = useState(false)

  useEffect(() => {
    const hash = new URLSearchParams(location.search.slice(1))
    const code = hash.get("code")
    const state = hash.get("state")

    if (code && state) {
      callback(code, state)
    }

    if (initializing.current) {
      initializing.current = false
      auth()
    }
  }, [])

  const toggleMobileMenu = () => {
    setIsMobileMenuOpen(!isMobileMenuOpen);
  };

  const closeMobileMenu = () => {
    setIsMobileMenuOpen(false);
  };

  async function auth() {
    const storedAccess = localStorage.getItem("accessToken")
    if (storedAccess) {
      accessToken.current = storedAccess
      setHasToken(true)
      await user(storedAccess)
      setIsAuthenticating(false)
      return
    }

    const token = await getToken()

    if (token) {
      await user(token)
    } else {
      setHasToken(false)
    }

    setIsAuthenticating(false)
  }

  async function callback(code: string, state: string) {
    const challenge = JSON.parse(sessionStorage.getItem("challenge")!)
    if (code) {
      if (state === challenge.state && challenge.verifier) {
        const exchanged = await client.exchange(
          code,
          location.origin,
          challenge.verifier,
        )
        if (!exchanged.err) {
          const tokens = (exchanged as any).tokens
          if (tokens?.access) {
            accessToken.current = tokens.access
            setHasToken(true)
          }
          if (tokens?.refresh) {
            localStorage.setItem("refresh", tokens.refresh)
          }
        }
      }
      window.location.replace("/")
    }
  }
  
  async function user(token: string) {
    try {
      const decoded = jwtDecode<JWTPayload>(token);
      accessToken.current = token
      localStorage.setItem("accessToken", token);
      const details = await userApi.getUser(decoded.properties.username);
      localStorage.setItem('currentUser', JSON.stringify(details));

      setHasToken(true)
      const displayName = decoded.properties.email || decoded.properties.username || undefined
      setUsername(displayName);
      setAvatar(decoded.properties.avatar ?? undefined);
    } catch (error) {
      console.error("Failed to decode token", error)
      clearTokens()
    }
  }

  async function login() {
    const token = await getToken()
    if (!token) {
      const { challenge, url } = await client.authorize(
        location.origin,
        "code",
        {
          pkce: true,
        },
      )
      sessionStorage.setItem("challenge", JSON.stringify(challenge))
      location.href = url
    } else {
      await user(token)
    }
  }

  async function getToken() {
    const refresh = localStorage.getItem("refresh")
    if (!refresh) return
    const next = await client.refresh(refresh, {
      access: accessToken.current,
    })
    if (next.err) {
      clearTokens()
      return
    }

    const tokens = (next as any).tokens

    if (!tokens || !tokens.access) {
      setHasToken(!!accessToken.current)
      return accessToken.current
    }

    if (tokens.refresh) {
      localStorage.setItem("refresh", tokens.refresh)
    }
    accessToken.current = tokens.access
    setHasToken(true)

    return tokens.access
  }

  function clearTokens() {
    accessToken.current = undefined
    setUsername(undefined)
    setAvatar(undefined)
    setHasToken(false)
    localStorage.removeItem("refresh")
    localStorage.removeItem("accessToken")
    sessionStorage.removeItem("challenge")
  }

  function logout() {
    clearTokens()
  }

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
                {isAuthenticating ? (
                  <div className="px-4 py-3 text-primary font-mono text-xs animate-pulse">» Loading...</div>
                ) : (
                  <div>
                    {hasToken ? (
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
                        <span>{username ?? 'Profile'}</span>
                      </NavLink>
                    ) : (
                      <button 
                        className="w-full flex items-center gap-3 py-3 px-4 text-muted-foreground hover:text-primary dark:hover:text-cyan-400 hover:bg-muted/50 dark:hover:bg-cyan-950/30 font-mono text-xs uppercase tracking-wider transition-all"
                        onClick={() => {
                          login();
                          closeMobileMenu();
                        }}
                      >
                        <User className="w-4 h-4" />
                        <span>Login</span>
                      </button>
                    )}

                    {hasToken && (
                      <button
                        className="w-full flex items-center gap-3 py-3 px-4 text-muted-foreground hover:text-primary dark:hover:text-cyan-400 hover:bg-muted/50 dark:hover:bg-cyan-950/30 font-mono text-xs uppercase tracking-wider transition-all"
                        onClick={() => {
                          logout()
                          closeMobileMenu()
                        }}
                      >
                        <LogOut className="w-4 h-4" />
                        <span>Logout</span>
                      </button>
                    )}
                  </div>
                )}
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
          {isAuthenticating ? (
            <div className="text-primary font-mono text-xs animate-pulse">» Loading...</div>
          ) : (
            <div className="content">
              {hasToken ? (
                <div className="flex flex-col gap-2">
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
                    {!isCollapsed && <span className="truncate">{username ?? 'Profile'}</span>}
                  </NavLink>
                  <button
                    className="w-full flex items-center gap-3 px-3 py-2.5 border-l-2 border-transparent text-muted-foreground hover:text-primary dark:hover:text-cyan-400 hover:bg-muted/50 dark:hover:bg-cyan-950/30 hover:border-primary dark:hover:border-cyan-700 font-mono text-xs uppercase tracking-wider transition-all"
                    onClick={logout}
                  >
                    <LogOut className="w-4 h-4" />
                    {!isCollapsed && <span>Logout</span>}
                  </button>
                </div>
              ) : (
                <button 
                  className="w-full flex items-center gap-3 px-3 py-2.5 border-l-2 border-transparent text-muted-foreground hover:text-primary dark:hover:text-cyan-400 hover:bg-muted/50 dark:hover:bg-cyan-950/30 hover:border-primary dark:hover:border-cyan-700 font-mono text-xs uppercase tracking-wider transition-all"
                  onClick={login}
                >
                  <User className="w-4 h-4" />
                  {!isCollapsed && <span>Login</span>}
                </button>
              )}
            </div>
          )}
          
          {/* Desktop Theme Toggle */}
          <div className="pt-2 border-t border-sidebar-border">
            <ThemeToggle isCollapsed={isCollapsed} />
          </div>
        </div>
      </div>
    </>
  );
}
