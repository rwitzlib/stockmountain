import { useState } from 'react';
import { Link, useParams, useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { StrategyForm } from '../../components/forms/StrategyForm';
import { Strategy } from '../../types/strategy';
import { Button } from '../../components/ui/button';
import { ArrowLeft, Settings } from 'lucide-react';
import { toast } from '../../hooks/use-toast';
import { strategyApi } from '../../api/strategyApi';

const StrategySettingsPage = () => {
  const { strategyId } = useParams<{ strategyId: string }>();
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  // Fetch strategy details
  const { data: strategy, isLoading: isLoadingStrategy } = useQuery({
    queryKey: ['strategy', strategyId],
    queryFn: () => strategyApi.getStrategy(strategyId!),
    enabled: !!strategyId,
  });

  const updateStrategyMutation = useMutation({
    mutationFn: ({ id, data }: { id: string; data: Partial<Strategy> }) =>
      strategyApi.updateStrategy(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['strategy', strategyId] });
      toast({
        title: "Success",
        description: "Strategy settings updated successfully",
      });
      // Navigate back to strategy detail page
      navigate(`/optimus/strategy/${strategyId}`);
    },
    onError: () => {
      toast({
        title: "Error",
        description: "Failed to update strategy settings",
        variant: "destructive",
      });
    }
  });

  const handleUpdateStrategy = (data: Strategy) => {
    if (strategyId) {
      updateStrategyMutation.mutate({ id: strategyId, data });
    }
  };

  if (isLoadingStrategy) {
    return (
      <div className="min-h-screen bg-background p-4 md:p-8">
        <div className="max-w-4xl mx-auto">
          <div className="text-center py-8">Loading strategy...</div>
        </div>
      </div>
    );
  }

  if (!strategy) {
    return (
      <div className="min-h-screen bg-background p-4 md:p-8">
        <div className="max-w-4xl mx-auto">
          <div className="text-center py-8">Strategy not found</div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-background p-4 md:p-8 pt-20 md:pt-8">
      <div className="max-w-4xl mx-auto space-y-8">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-4">
            <Button
              variant="outline"
              size="sm"
              className="bg-white/50 backdrop-blur-sm hover:bg-white/80 border-purple-200 hover:border-purple-300"
              onClick={() => navigate(`/optimus/strategy/${strategyId}`)}
            >
              <ArrowLeft className="h-4 w-4 mr-1" />
              Back to Strategy
            </Button>
            <div>
              <h1 className="text-2xl font-bold gradient-heading flex items-center gap-2">
                <Settings className="h-6 w-6" />
                {strategy.name || 'Unnamed Strategy'} - Settings
              </h1>
              <p className="text-gray-600 mt-1">Configure your strategy settings and parameters</p>
            </div>
          </div>
        </div>

        {/* Settings Form */}
        <div className="rounded-lg p-6 shadow-sm border border-border bg-card text-card-foreground">
          <StrategyForm
            initialData={strategy}
            onSubmit={handleUpdateStrategy}
            isLoading={updateStrategyMutation.isPending}
          />
        </div>
      </div>
    </div>
  );
};

export default StrategySettingsPage;
