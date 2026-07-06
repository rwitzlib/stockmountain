import { useEffect } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { Button } from '../../components/ui/button';
import { ChevronLeft, Plus, Lock } from 'lucide-react';
import { Strategy } from '../../types/strategy';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { StrategyCard } from '../../components/StrategyCard';
import { toast } from '../../hooks/use-toast';
import { strategyApi } from '../../api/strategyApi';

import { useUser } from '@clerk/react';

const Dashboard = () => {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const location = useLocation();
  const { isLoaded, isSignedIn } = useUser();

  // Redirect once Clerk has loaded and the user is definitely signed out
  useEffect(() => {
    if (isLoaded && !isSignedIn) {
      toast({
        title: "Authentication Required",
        description: "Please log in to view your personal dashboard",
        variant: "destructive",
      });
      navigate('/optimus');
      return;
    }
  }, [isLoaded, isSignedIn, navigate]);

  // Handle navigation state for creating strategy from backtest
  useEffect(() => {
    if (location.state?.createStrategy && isSignedIn) {
      const strategyData = location.state.initialStrategyData;
      
      // Navigate to the new strategy editor page with initial data
      navigate('/optimus/strategy/new', { 
        state: { initialData: strategyData },
        replace: true 
      });
    }
  }, [location.state, navigate, isSignedIn]);

  const { data: myStrategies, isLoading: isLoadingStrategies } = useQuery({
    queryKey: ['myStrategies'],
    queryFn: strategyApi.getMyStrategies,
    enabled: !!isSignedIn // Only fetch if authenticated
  });


  const updateBotMutation = useMutation({
    mutationFn: ({ id, data }: { id: string; data: Partial<Strategy> }) => 
      strategyApi.updateStrategy(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['myStrategies'] });
      toast({
        title: "Success",
        description: "Trading strategy updated successfully",
      });
    },
    onError: (error) => {
      toast({
        title: "Error",
        description: "Failed to update trading strategy",
        variant: "destructive",
      });
    }
  });

  const deleteBotMutation = useMutation({
    mutationFn: strategyApi.deleteStrategy,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['myStrategies'] });
      toast({
        title: "Success",
        description: "Trading strategy deleted successfully",
      });
    },
    onError: (error) => {
      toast({
        title: "Error",
        description: "Failed to delete trading strategy",
        variant: "destructive",
      });
    }
  });

  const handleUpdateBot = (id: string, data: Partial<Strategy>) => {
    updateBotMutation.mutate({ id, data });
  };

  const handleDeleteBot = (id: string) => {
    deleteBotMutation.mutate(id);
  };

  // Don't render if not authenticated
  if (!isSignedIn) {
    return (
      <div className="min-h-screen bg-background">
        <div className="p-4 md:p-8 pt-20 md:pt-8">
          <div className="max-w-7xl mx-auto space-y-8">
            <div className="flex items-center gap-4 border-b border-border pb-4">
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
                <Lock className="h-6 w-6 text-red-600 dark:text-red-400 animate-pulse" />
                <h1 className="text-xl font-mono font-bold uppercase tracking-wider text-red-600 dark:text-red-400">⚠️ Authentication Required</h1>
              </div>
            </div>
            <div className="text-center py-12 bg-red-100/50 dark:bg-red-950/20 border border-red-300 dark:border-red-900">
              <p className="text-red-700 dark:text-red-400 font-mono text-sm mb-2">[ ACCESS DENIED ]</p>
              <p className="text-muted-foreground font-mono text-xs">{'>> '}Please log in to access your personal trading dashboard</p>
            </div>
          </div>
        </div>
      </div>
    );
  }

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
                <Lock className="h-5 w-5 text-green-600 dark:text-green-400 animate-pulse" />
                <h1 className="text-xl font-mono font-bold uppercase tracking-wider text-foreground"># My Trading Strategies</h1>
              </div>
            </div>
            
            <Button 
              onClick={() => navigate('/optimus/strategy/new')}
              className="bg-green-100 dark:bg-green-950 hover:bg-green-200 dark:hover:bg-green-900 text-green-700 dark:text-green-400 border border-green-300 dark:border-green-700 hover:border-green-400 dark:hover:border-green-500 gap-2 font-mono text-xs uppercase transition-all"
            >
              <Plus className="w-4 h-4" />
              New Strategy
            </Button>
          </div>
          
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {isLoadingStrategies ? (
              <div className="col-span-full text-center py-12 bg-card border border-border">
                <p className="text-primary font-mono text-sm animate-pulse">» LOADING STRATEGIES...</p>
              </div>
            ) : myStrategies?.length === 0 ? (
              <div className="col-span-full text-center py-12 bg-card border border-border">
                <p className="text-muted-foreground font-mono text-sm mb-2">[ NO STRATEGIES FOUND ]</p>
                <p className="text-muted-foreground/60 font-mono text-xs">{'>> '}Create your first trading strategy to get started</p>
              </div>
            ) : (
              myStrategies?.map((strategy: Strategy) => (
                <StrategyCard
                  key={strategy.id}
                  strategy={strategy}
                  onUpdate={handleUpdateBot}
                  onDelete={handleDeleteBot}
                  from="/optimus/dashboard"
                />
              ))
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

export default Dashboard; 