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
                  className="bg-background hover:bg-muted border-border hover:border-primary text-muted-foreground hover:text-primary transition-all"
                >
                  <ChevronLeft className="h-4 w-4 mr-1" />
                  <span className="font-mono text-xs uppercase">Back</span>
                </Button>
              </Link>
              <div className="flex items-center gap-2">
                <Globe className="h-6 w-6 text-primary dark:text-cyan-400 animate-pulse" />
                <h1 className="text-xl font-mono font-bold uppercase tracking-wider text-foreground"># Public Trading Strategies</h1>
              </div>
            </div>
            
            <div className="text-xs font-mono uppercase text-muted-foreground">
              {'>> '}Community Shared Strategies
            </div>
          </div>
          
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {isLoadingStrategies ? (
              <div className="col-span-full text-center py-12 bg-card border border-border">
                <p className="text-primary font-mono text-sm animate-pulse">» LOADING PUBLIC STRATEGIES...</p>
              </div>
            ) : publicStrategies?.length === 0 ? (
              <div className="col-span-full text-center py-12 bg-card border border-border">
                <p className="text-muted-foreground font-mono text-sm mb-2">[ NO PUBLIC STRATEGIES ]</p>
                <p className="text-muted-foreground/60 font-mono text-xs">{'>> '}No strategies have been shared publicly yet</p>
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