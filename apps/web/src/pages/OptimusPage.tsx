import React from 'react';
import { NavLink, Outlet } from 'react-router-dom';
import { Globe, Lock } from 'lucide-react';
import { useUser } from '@clerk/react';

export function OptimusPage() {
  const { isSignedIn } = useUser();
  const userIsAuthenticated = !!isSignedIn;

  return (
    <div className="min-h-screen bg-background p-4 md:p-8 pt-20 md:pt-8">
      <div className="border-b border-border pb-4 mb-6">
        <h1 className="text-xl font-mono font-bold uppercase tracking-wider text-foreground"># Optimus Trading System</h1>
        <p className="text-xs font-mono text-muted-foreground mt-1">{'>> '}Automated Strategy Execution Platform</p>
      </div>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
        <NavLink 
          to="/optimus/public-dashboard"
          className="relative block p-6 bg-card border border-border hover:border-primary dark:hover:border-cyan-700 hover:shadow-lg hover:shadow-primary/20 dark:hover:shadow-cyan-900/20 transition-all duration-200 hover:-translate-y-[2px] group"
        >
          <div className="absolute inset-y-0 left-0 w-1 bg-primary dark:bg-cyan-700 group-hover:bg-primary/80 dark:group-hover:bg-cyan-500 transition-colors" aria-hidden="true" />
          <div className="flex items-center gap-3 mb-3">
            <Globe className="h-6 w-6 text-primary dark:text-cyan-400 group-hover:animate-pulse" />
            <h2 className="text-base font-mono font-bold uppercase tracking-wider text-foreground">Public Dashboard</h2>
          </div>
          <p className="text-xs font-mono text-muted-foreground pl-9">{'>> '}Browse trading strategies shared by the community</p>
        </NavLink>
        {userIsAuthenticated && (
          <NavLink 
            to="/optimus/dashboard"
            className="relative block p-6 bg-card border border-border hover:border-green-500 dark:hover:border-green-700 hover:shadow-lg hover:shadow-green-500/20 dark:hover:shadow-green-900/20 transition-all duration-200 hover:-translate-y-[2px] group"
          >
            <div className="absolute inset-y-0 left-0 w-1 bg-green-500 dark:bg-green-700 group-hover:bg-green-400 dark:group-hover:bg-green-500 transition-colors" aria-hidden="true" />
            <div className="flex items-center gap-3 mb-3">
              <Lock className="h-6 w-6 text-green-600 dark:text-green-400 group-hover:animate-pulse" />
              <h2 className="text-base font-mono font-bold uppercase tracking-wider text-foreground">My Dashboard</h2>
            </div>
            <p className="text-xs font-mono text-muted-foreground pl-9">{'>> '}View and manage your personal trading strategies</p>
          </NavLink>
        )}
      </div>
      <Outlet />
    </div>
  );
} 