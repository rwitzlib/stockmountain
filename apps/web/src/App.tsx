import { BrowserRouter, Routes, Route, Outlet, Navigate } from 'react-router-dom';
import type { ReactNode } from 'react';
import { SignIn, SignUp, useUser } from '@clerk/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Sidebar } from './components/layout/Sidebar';
import { HomePage } from './pages/HomePage';
import { LandingPage } from './pages/LandingPage';
import { routes } from './routes';
import Profile from './pages/Profile';
import { OptimusPage } from './pages/OptimusPage';
import Dashboard from './pages/optimus/DashboardPage';
import PublicDashboard from './pages/optimus/PublicDashboardPage';
import CalendarPage from './pages/optimus/CalendarPage';
import TradesPage from './pages/optimus/TradesPage';
import TradeDetail from './pages/optimus/TradeDetailPage';
import StrategyDetailPage from './pages/optimus/StrategyDetailPage';
import StrategyEditorPage from './pages/optimus/StrategyEditorPage';
import StrategyOptimizePage from './pages/optimus/StrategyOptimizePage';
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
      <main className="flex-1">{children ?? <Outlet />}</main>
    </div>
  );
}

/** `/` — marketing landing for visitors, module launcher for signed-in users. */
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
              <Route path="/optimus" element={<Outlet />}>
                <Route index element={<OptimusPage />} />
                <Route path="public-dashboard" element={<PublicDashboard />} />
                <Route path="dashboard" element={<Dashboard />} />
                <Route path="strategy/new" element={<StrategyEditorPage />} />
                <Route path="strategy/:strategyId" element={<StrategyDetailPage />} />
                <Route path="strategy/:strategyId/edit" element={<StrategyEditorPage />} />
                <Route path="strategy/:strategyId/optimize" element={<StrategyOptimizePage />} />
                {/* Legacy settings URL — the editor is the single edit surface now */}
                <Route
                  path="strategy/:strategyId/settings"
                  element={<Navigate to="../edit" relative="path" replace />}
                />
                <Route path="calendar" element={<CalendarPage />} />
                <Route path="trades" element={<TradesPage />} />
                <Route path="trade/:id" element={<TradeDetail />} />
              </Route>
          </Route>
        </Routes>
        <Toaster />
      </BrowserRouter>
    </QueryClientProvider>
  );
}
