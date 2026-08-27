import { BrowserRouter, Routes, Route, Outlet, Navigate, useLocation } from 'react-router-dom';
import type { ReactNode } from 'react';
import { SignIn, SignUp, useUser } from '@clerk/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Sidebar } from './components/layout/Sidebar';
import { HomePage } from './pages/HomePage';
import { LandingPage } from './pages/LandingPage';
import { routes } from './routes';
import Profile from './pages/Profile';
import Dashboard from './pages/strategies/DashboardPage';
import PublicDashboard from './pages/strategies/PublicDashboardPage';
import CalendarPage from './pages/strategies/CalendarPage';
import TradesPage from './pages/strategies/TradesPage';
import TradeDetail from './pages/strategies/TradeDetailPage';
import StrategyDetailPage from './pages/strategies/StrategyDetailPage';
import StrategyEditorPage from './pages/strategies/StrategyEditorPage';
import StrategyOptimizePage from './pages/strategies/StrategyOptimizePage';
import { Toaster } from './components/ui/toaster';

const queryClient = new QueryClient();

function AuthPage({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-screen items-center justify-center p-6">
      {children}
    </div>
  );
}

/** Sidebar + main-content shell for the app itself (everything except the marketing page). */
function AppShell({ children }: { children?: ReactNode }) {
  return (
    <div className="flex">
      <Sidebar />
      <main className="flex-1 min-w-0">{children ?? <Outlet />}</main>
    </div>
  );
}

/** `/` — marketing landing for visitors, dashboard for signed-in users. */
function RootRoute() {
  const { isLoaded, isSignedIn } = useUser();
  if (!isLoaded) return null;
  return isSignedIn ? (
    <AppShell>
      <HomePage />
    </AppShell>
  ) : (
    <LandingPage />
  );
}

/** Old /optimus/* URLs (bookmarks, shared links) → their /strategies/* equivalents. */
function LegacyOptimusRedirect() {
  const location = useLocation();
  const rest = location.pathname.replace(/^\/optimus\/?/, '');
  let target: string;
  if (rest === '' || rest === 'dashboard') {
    target = '/strategies';
  } else if (rest === 'public-dashboard') {
    target = '/strategies/community';
  } else if (rest.startsWith('strategy/')) {
    target = `/strategies/${rest.slice('strategy/'.length)}`;
  } else {
    target = `/strategies/${rest}`;
  }
  return <Navigate to={target + location.search} replace />;
}

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<RootRoute />} />
          <Route element={<AppShell />}>
              <Route
                path="/sign-in/*"
                element={(
                  <AuthPage>
                    <SignIn path="/sign-in" routing="path" signUpUrl="/sign-up" />
                  </AuthPage>
                )}
              />
              <Route
                path="/sign-up/*"
                element={(
                  <AuthPage>
                    <SignUp path="/sign-up" routing="path" signInUrl="/sign-in" />
                  </AuthPage>
                )}
              />
              {routes.map((route) => {
                if (route.children) {
                  return (
                    <Route
                      key={route.path}
                      path={route.path}
                      element={route.element}
                    >
                      {route.children.map((childRoute) => (
                        <Route
                          key={childRoute.path}
                          path={childRoute.path}
                          element={childRoute.element}
                          index={childRoute.index}
                        />
                      ))}
                    </Route>
                  );
                }
                return (
                  <Route
                    key={route.path}
                    path={route.path}
                    element={route.element}
                    index={route.index}
                  />
                );
              })}
              <Route path="/profile" element={<Profile />} />
              <Route path="/strategies" element={<Outlet />}>
                <Route index element={<Dashboard />} />
                <Route path="community" element={<PublicDashboard />} />
                <Route path="trades" element={<TradesPage />} />
                <Route path="calendar" element={<CalendarPage />} />
                <Route path="trade/:id" element={<TradeDetail />} />
                <Route path="new" element={<StrategyEditorPage />} />
                <Route path=":strategyId" element={<StrategyDetailPage />} />
                <Route path=":strategyId/edit" element={<StrategyEditorPage />} />
                <Route path=":strategyId/optimize" element={<StrategyOptimizePage />} />
                {/* Legacy settings URL — the editor is the single edit surface now */}
                <Route
                  path=":strategyId/settings"
                  element={<Navigate to="../edit" relative="path" replace />}
                />
              </Route>
              <Route path="/optimus/*" element={<LegacyOptimusRedirect />} />
          </Route>
        </Routes>
        <Toaster />
      </BrowserRouter>
    </QueryClientProvider>
  );
}
