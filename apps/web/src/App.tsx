import { BrowserRouter, Routes, Route, Outlet } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Sidebar } from './components/layout/Sidebar';
import { HomePage } from './pages/HomePage';
import { routes } from './routes';
import Profile from './pages/Profile';
import { OptimusPage } from './pages/OptimusPage';
import Dashboard from './pages/optimus/DashboardPage';
import PublicDashboard from './pages/optimus/PublicDashboardPage';
import CalendarPage from './pages/optimus/CalendarPage';
import TradesPage from './pages/optimus/TradesPage';
import TradeDetail from './pages/optimus/TradeDetailPage';
import StrategyDetailPage from './pages/optimus/StrategyDetailPage';
import StrategySettingsPage from './pages/optimus/StrategySettingsPage';
import StrategyEditorPage from './pages/optimus/StrategyEditorPage';
import StrategyOptimizePage from './pages/optimus/StrategyOptimizePage';
import { Toaster } from './components/ui/toaster';

const queryClient = new QueryClient();

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <div className="flex">
          <Sidebar />
          <main className="flex-1">
            <Routes>
              <Route path="/" element={<HomePage />} />
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
                <Route path="strategy/:strategyId/settings" element={<StrategySettingsPage />} />
                <Route path="calendar" element={<CalendarPage />} />
                <Route path="trades" element={<TradesPage />} />
                <Route path="trade/:id" element={<TradeDetail />} />
              </Route>
            </Routes>
          </main>
        </div>
        <Toaster />
      </BrowserRouter>
    </QueryClientProvider>
  );
}
