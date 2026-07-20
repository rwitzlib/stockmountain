import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Button } from '../../components/ui/button';
import { ChevronLeft, Globe } from 'lucide-react';
import { Strategy } from '../../types/strategy';
import { useQuery } from '@tanstack/react-query';
import { StrategyCard } from '../../components/StrategyCard';
import { strategyApi } from '../../api/strategyApi';

const PublicDashboard = () => {
  const { data: publicStrategies, isLoading: isLoadingStrategies } = useQuery({
    queryKey: ['publicStrategies'],
    queryFn: strategyApi.getPublicStrategies
  });

  // Public dashboard doesn't allow editing/deleting strategies
  const handleUpdateStrategy = () => {
    // No-op for public dashboard
  };

  const handleDeleteStrategy = () => {
    // No-op for public dashboard
  };

  return (
    <div className="min-h-screen bg-background">
      <div className="p-4 md:p-8 pt-20 md:pt-8">
        <div className="max-w-7xl mx-auto space-y-6">
          <div className="flex items-center justify-between border-b border-border pb-4">
            <div className="flex items-center gap-4">
              <Link to="/optimus">
                <Button 
                  variant="outline" 
                  size="sm"
                  className="text-muted-foreground hover:bg-accent hover:text-foreground transition-colors"
                >
                  <ChevronLeft className="h-4 w-4 mr-1" />
                  <span className="text-xs">Back</span>
                </Button>
              </Link>
              <div className="flex items-center gap-2">
                <Globe className="h-6 w-6 text-muted-foreground" />
                <h1 className="text-xl font-semibold tracking-tight text-foreground">Public Trading Strategies</h1>
              </div>
            </div>
            
            <div className="text-sm text-muted-foreground">
              Community shared strategies
            </div>
          </div>
          
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {isLoadingStrategies ? (
              <div className="col-span-full text-center py-12 rounded-xl border border-border/80 bg-card">
                <p className="text-muted-foreground text-sm animate-pulse">Loading public strategies…</p>
              </div>
            ) : publicStrategies?.length === 0 ? (
              <div className="col-span-full text-center py-12 rounded-xl border border-border/80 bg-card">
                <p className="text-foreground text-sm font-medium mb-2">No public strategies</p>
                <p className="text-muted-foreground text-xs">No strategies have been shared publicly yet</p>
              </div>
            ) : (
              publicStrategies?.map((strategy: Strategy) => (
                <StrategyCard
                  key={strategy.id}
                  strategy={strategy}
                  onUpdate={handleUpdateStrategy}
                  onDelete={handleDeleteStrategy}
                  readOnly={true}
                  from="/optimus/public-dashboard"
                />
              ))
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

export default PublicDashboard; 