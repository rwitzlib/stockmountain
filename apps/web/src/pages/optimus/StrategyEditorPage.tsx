import { useState, useEffect } from 'react';
import { useNavigate, useParams, useLocation, Link } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { 
  Strategy, 
  StrategyStateType, 
  VisibilityType, 
  TradeType, 
  IntegrationType 
} from '../../types/strategy';
import { PositionSettingsForm } from '../../components/forms/strategy/PositionSettingsForm';
import { ExitSettingsForm } from '../../components/forms/strategy/ExitSettingsForm';
import { EntrySettingsForm } from '../../components/forms/strategy/EntrySettingsForm';
import { Switch } from '../../components/ui/switch';
import { Button } from '../../components/ui/button';
import { Card } from '../../components/ui/card';
import { strategyApi } from '../../api/strategyApi';
import { toast } from '../../hooks/use-toast';
import { useUser } from '@clerk/react';
import { 
  ChevronLeft, 
  Settings2, 
  DoorOpen, 
  Filter, 
  Loader2,
  Zap,
  Globe,
  EyeOff,
  Save,
  CheckCircle2,
  AlertCircle
} from 'lucide-react';

const TRADE_TYPES: { value: TradeType; label: string; icon: React.ReactNode; description: string }[] = [
  { value: 'Paper', label: 'Paper Trading', icon: <span className="text-2xl">📝</span>, description: 'Test strategies with simulated trades' },
  { value: 'Live', label: 'Live Trading', icon: <Zap className="w-6 h-6 text-amber-500" />, description: 'Execute real trades with your broker' },
];

const INTEGRATION_TYPES: { value: IntegrationType; label: string; description: string }[] = [
  { value: 'Default', label: 'Default', description: 'Internal paper trading engine' },
  { value: 'Schwab', label: 'Charles Schwab', description: 'Connect your Schwab account' },
  { value: 'Fidelity', label: 'Fidelity', description: 'Connect your Fidelity account' },
  { value: 'ETrade', label: 'E*Trade', description: 'Connect your E*Trade account' },
];

const defaultFormData: Strategy = {
  name: '',
  state: 'Inactive',
  visibility: 'Private',
  type: 'Paper',
  integration: 'Default',
  positionSettings: {
    startingBalance: 10000,
    maxConcurrentPositions: 1,
    allowSimultaneous: false,
    model: {
      type: 'Fixed',
      size: 1000,
    },
  },
  exitSettings: {},
  entrySettings: {
    filters: [],
  },
};

type SectionId = 'general' | 'position' | 'exit' | 'entry';

interface Section {
  id: SectionId;
  label: string;
  icon: React.ReactNode;
  description: string;
}

const SECTIONS: Section[] = [
  { id: 'general', label: 'General', icon: <Settings2 className="w-5 h-5" />, description: 'Basic strategy configuration' },
  { id: 'position', label: 'Position Sizing', icon: <span className="text-lg">💰</span>, description: 'How to size and manage positions' },
  { id: 'exit', label: 'Exit Rules', icon: <DoorOpen className="w-5 h-5" />, description: 'When and how to exit positions' },
  { id: 'entry', label: 'Entry Conditions', icon: <Filter className="w-5 h-5" />, description: 'Criteria for entering trades' },
];

const StrategyEditorPage = () => {
  const navigate = useNavigate();
  const location = useLocation();
  const queryClient = useQueryClient();
  const { strategyId } = useParams<{ strategyId: string }>();
  
  const isEditMode = !!strategyId;
  const [activeSection, setActiveSection] = useState<SectionId>('general');
  const [formData, setFormData] = useState<Strategy>(defaultFormData);
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);
  const { isLoaded, isSignedIn } = useUser();

  // Redirect once Clerk has loaded and the user is definitely signed out
  useEffect(() => {
    if (isLoaded && !isSignedIn) {
      toast({
        title: "Authentication Required",
        description: "Please log in to create or edit strategies",
        variant: "destructive",
      });
      navigate('/optimus');
    }
  }, [isLoaded, isSignedIn, navigate]);

  // Fetch existing strategy if editing
  const { data: existingStrategy, isLoading: isLoadingStrategy } = useQuery({
    queryKey: ['strategy', strategyId],
    queryFn: () => strategyApi.getStrategy(strategyId!),
    enabled: isEditMode && !!isSignedIn,
  });

  // Initialize form with existing strategy data or from navigation state
  useEffect(() => {
    if (existingStrategy) {
      setFormData({
        ...defaultFormData,
        ...existingStrategy,
        positionSettings: {
          ...defaultFormData.positionSettings,
          ...existingStrategy.positionSettings,
          model: {
            ...defaultFormData.positionSettings.model,
            ...existingStrategy.positionSettings?.model,
          },
        },
        exitSettings: {
          ...defaultFormData.exitSettings,
          ...existingStrategy.exitSettings,
        },
        entrySettings: {
          ...defaultFormData.entrySettings,
          ...existingStrategy.entrySettings,
          filters: existingStrategy.entrySettings?.filters || [],
        },
      });
    } else if (location.state?.initialData) {
      // Handle initial data from navigation (e.g., from backtest)
      const initialData = location.state.initialData;
      setFormData({
        ...defaultFormData,
        ...initialData,
        positionSettings: {
          ...defaultFormData.positionSettings,
          ...initialData.positionSettings,
          model: {
            ...defaultFormData.positionSettings.model,
            ...initialData.positionSettings?.model,
          },
        },
        exitSettings: {
          ...defaultFormData.exitSettings,
          ...initialData.exitSettings,
        },
        entrySettings: {
          ...defaultFormData.entrySettings,
          ...initialData.entrySettings,
          filters: initialData.entrySettings?.filters || [],
        },
      });
    }
  }, [existingStrategy, location.state]);

  // Track unsaved changes
  useEffect(() => {
    setHasUnsavedChanges(true);
  }, [formData]);

  // Create mutation
  const createMutation = useMutation({
    mutationFn: strategyApi.createStrategy,
    onSuccess: (response) => {
      queryClient.invalidateQueries({ queryKey: ['myStrategies'] });
      toast({
        title: "Strategy Created",
        description: `"${formData.name}" has been created successfully`,
      });
      navigate(`/optimus/strategy/${response.id}`);
    },
    onError: (error) => {
      toast({
        title: "Error",
        description: "Failed to create strategy. Please try again.",
        variant: "destructive",
      });
    },
  });

  // Update mutation
  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: string; data: Partial<Strategy> }) =>
      strategyApi.updateStrategy(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['strategy', strategyId] });
      queryClient.invalidateQueries({ queryKey: ['myStrategies'] });
      setHasUnsavedChanges(false);
      toast({
        title: "Strategy Updated",
        description: `"${formData.name}" has been saved successfully`,
      });
    },
    onError: (error) => {
      toast({
        title: "Error",
        description: "Failed to update strategy. Please try again.",
        variant: "destructive",
      });
    },
  });

  const handleSubmit = () => {
    if (!formData.name.trim()) {
      toast({
        title: "Validation Error",
        description: "Please enter a strategy name",
        variant: "destructive",
      });
      setActiveSection('general');
      return;
    }

    if (isEditMode && strategyId) {
      updateMutation.mutate({ id: strategyId, data: formData });
    } else {
      createMutation.mutate(formData);
    }
  };

  const isLoading = createMutation.isPending || updateMutation.isPending;
  const isValid = formData.name.trim().length > 0;

  // Get section completion status
  const getSectionStatus = (sectionId: SectionId): 'complete' | 'incomplete' | 'empty' => {
    switch (sectionId) {
      case 'general':
        return formData.name.trim() ? 'complete' : 'incomplete';
      case 'position':
        return formData.positionSettings.startingBalance > 0 ? 'complete' : 'incomplete';
      case 'exit':
        return (formData.exitSettings.stopLoss || formData.exitSettings.takeProfit || formData.exitSettings.timedExit) 
          ? 'complete' : 'empty';
      case 'entry':
        return formData.entrySettings.filters.length > 0 ? 'complete' : 'empty';
      default:
        return 'empty';
    }
  };

  if (isEditMode && isLoadingStrategy) {
    return (
      <div className="min-h-screen bg-background flex items-center justify-center">
        <div className="text-center">
          <Loader2 className="w-8 h-8 animate-spin text-primary mx-auto mb-4" />
          <p className="text-muted-foreground font-mono text-sm">Loading strategy...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-background">
      {/* Header */}
      <div className="sticky top-0 z-10 bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60 border-b border-border">
        <div className="max-w-7xl mx-auto px-4 md:px-8 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-4">
              <Button
                variant="outline"
                size="sm"
                onClick={() => navigate(-1)}
                className="border-border hover:border-primary"
              >
                <ChevronLeft className="w-4 h-4 mr-1" />
                Back
              </Button>
              <div>
                <h1 className="text-xl font-bold text-foreground">
                  {isEditMode ? 'Edit Strategy' : 'Create New Strategy'}
                </h1>
                <p className="text-sm text-muted-foreground">
                  {isEditMode ? `Editing "${existingStrategy?.name || formData.name}"` : 'Configure your automated trading strategy'}
                </p>
              </div>
            </div>

            <div className="flex items-center gap-3">
              {hasUnsavedChanges && (
                <span className="text-xs text-amber-600 dark:text-amber-400 flex items-center gap-1">
                  <AlertCircle className="w-3 h-3" />
                  Unsaved changes
                </span>
              )}
              <Button
                onClick={handleSubmit}
                disabled={isLoading || !isValid}
                className="bg-primary hover:bg-primary/90"
              >
                {isLoading ? (
                  <>
                    <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                    Saving...
                  </>
                ) : (
                  <>
                    <Save className="w-4 h-4 mr-2" />
                    {isEditMode ? 'Save Changes' : 'Create Strategy'}
                  </>
                )}
              </Button>
            </div>
          </div>
        </div>
      </div>

      {/* Main Content */}
      <div className="max-w-7xl mx-auto px-4 md:px-8 py-6">
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          {/* Sidebar Navigation */}
          <div className="lg:col-span-3">
            <Card className="p-4 sticky top-24">
              <h3 className="text-xs font-mono uppercase tracking-wider text-muted-foreground mb-4">
                Configuration Sections
              </h3>
              <nav className="space-y-1">
                {SECTIONS.map((section) => {
                  const status = getSectionStatus(section.id);
                  return (
                    <button
                      key={section.id}
                      onClick={() => setActiveSection(section.id)}
                      className={`w-full flex items-center gap-3 px-3 py-3 rounded-lg text-left transition-all ${
                        activeSection === section.id
                          ? 'bg-primary/10 border border-primary/30 text-primary'
                          : 'hover:bg-muted border border-transparent'
                      }`}
                    >
                      <div className={`${activeSection === section.id ? 'text-primary' : 'text-muted-foreground'}`}>
                        {section.icon}
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center justify-between">
                          <span className={`text-sm font-medium ${
                            activeSection === section.id ? 'text-primary' : 'text-foreground'
                          }`}>
                            {section.label}
                          </span>
                          {status === 'complete' && (
                            <CheckCircle2 className="w-4 h-4 text-green-500" />
                          )}
                          {status === 'incomplete' && (
                            <AlertCircle className="w-4 h-4 text-amber-500" />
                          )}
                        </div>
                        <p className="text-xs text-muted-foreground truncate">
                          {section.description}
                        </p>
                      </div>
                    </button>
                  );
                })}
              </nav>

              {/* Quick Summary */}
              <div className="mt-6 pt-4 border-t border-border">
                <h4 className="text-xs font-mono uppercase tracking-wider text-muted-foreground mb-3">
                  Summary
                </h4>
                <div className="space-y-2 text-xs">
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Mode</span>
                    <span className={formData.type === 'Live' ? 'text-amber-500' : 'text-primary'}>
                      {formData.type}
                    </span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Status</span>
                    <span className={formData.state === 'Active' ? 'text-green-500' : 'text-muted-foreground'}>
                      {formData.state}
                    </span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Balance</span>
                    <span className="text-foreground">
                      ${formData.positionSettings.startingBalance.toLocaleString()}
                    </span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Entry Filters</span>
                    <span className="text-foreground">
                      {formData.entrySettings.filters.length}
                    </span>
                  </div>
                </div>
              </div>
            </Card>
          </div>

          {/* Main Form Area */}
          <div className="lg:col-span-9">
            <Card className="p-6">
              {/* General Section */}
              {activeSection === 'general' && (
                <div className="space-y-8">
                  <div>
                    <h2 className="text-lg font-semibold text-foreground mb-1">General Configuration</h2>
                    <p className="text-sm text-muted-foreground">Basic settings for your trading strategy</p>
                  </div>

                  {/* Strategy Name */}
                  <div className="space-y-2">
                    <label htmlFor="name" className="block text-sm font-medium text-foreground">
                      Strategy Name <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="text"
                      id="name"
                      value={formData.name}
                      onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                      placeholder="e.g., RSI Pullback Strategy"
                      className="w-full px-4 py-3 rounded-lg border border-input bg-background text-foreground focus:outline-none focus:ring-2 focus:ring-ring focus:border-ring placeholder:text-muted-foreground/70"
                      autoFocus
                    />
                    <p className="text-xs text-muted-foreground">
                      Choose a descriptive name to identify this strategy
                    </p>
                  </div>

                  {/* Status & Visibility */}
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                    {/* Status */}
                    <div className="space-y-3">
                      <label className="block text-sm font-medium text-foreground">Strategy Status</label>
                      <div className={`flex items-center justify-between gap-4 p-4 rounded-lg border-2 transition-colors ${
                        formData.state === 'Active' 
                          ? 'bg-green-50 dark:bg-green-950/30 border-green-300 dark:border-green-700' 
                          : 'bg-muted/30 border-border'
                      }`}>
                        <div>
                          <div className="flex items-center gap-2">
                            <div className={`w-3 h-3 rounded-full ${
                              formData.state === 'Active' ? 'bg-green-500 animate-pulse' : 'bg-muted-foreground'
                            }`} />
                            <span className={`font-medium ${
                              formData.state === 'Active' ? 'text-green-700 dark:text-green-400' : 'text-muted-foreground'
                            }`}>
                              {formData.state === 'Active' ? 'Active' : 'Inactive'}
                            </span>
                          </div>
                          <p className="text-xs text-muted-foreground mt-1">
                            {formData.state === 'Active' 
                              ? 'Strategy will execute trades automatically' 
                              : 'Strategy will not execute any trades'}
                          </p>
                        </div>
                        <Switch
                          checked={formData.state === 'Active'}
                          onCheckedChange={(checked) => 
                            setFormData({ ...formData, state: checked ? 'Active' : 'Inactive' })
                          }
                        />
                      </div>
                    </div>

                    {/* Visibility */}
                    <div className="space-y-3">
                      <label className="block text-sm font-medium text-foreground">Visibility</label>
                      <div className={`flex items-center justify-between gap-4 p-4 rounded-lg border-2 transition-colors ${
                        formData.visibility === 'Public'
                          ? 'bg-blue-50 dark:bg-blue-950/30 border-blue-300 dark:border-blue-700'
                          : 'bg-muted/30 border-border'
                      }`}>
                        <div>
                          <div className="flex items-center gap-2">
                            {formData.visibility === 'Public' ? (
                              <Globe className="w-4 h-4 text-blue-600 dark:text-blue-400" />
                            ) : (
                              <EyeOff className="w-4 h-4 text-muted-foreground" />
                            )}
                            <span className={`font-medium ${
                              formData.visibility === 'Public' ? 'text-blue-700 dark:text-blue-400' : 'text-muted-foreground'
                            }`}>
                              {formData.visibility}
                            </span>
                          </div>
                          <p className="text-xs text-muted-foreground mt-1">
                            {formData.visibility === 'Public' 
                              ? 'Others can view this strategy' 
                              : 'Only you can see this strategy'}
                          </p>
                        </div>
                        <Switch
                          checked={formData.visibility === 'Public'}
                          onCheckedChange={(checked) =>
                            setFormData({ ...formData, visibility: checked ? 'Public' : 'Private' })
                          }
                        />
                      </div>
                    </div>
                  </div>

                  {/* Trade Type */}
                  <div className="space-y-3">
                    <label className="block text-sm font-medium text-foreground">Trading Mode</label>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                      {TRADE_TYPES.map((tradeType) => (
                        <button
                          key={tradeType.value}
                          type="button"
                          onClick={() => setFormData({ ...formData, type: tradeType.value })}
                          className={`p-5 rounded-lg border-2 text-left transition-all ${
                            formData.type === tradeType.value
                              ? tradeType.value === 'Live'
                                ? 'border-amber-500 bg-amber-50 dark:bg-amber-950/30'
                                : 'border-primary bg-primary/5 dark:bg-primary/10'
                              : 'border-border hover:border-muted-foreground/50'
                          }`}
                        >
                          <div className="flex items-center gap-3 mb-2">
                            {tradeType.icon}
                            <span className={`text-lg font-semibold ${
                              formData.type === tradeType.value
                                ? tradeType.value === 'Live'
                                  ? 'text-amber-700 dark:text-amber-400'
                                  : 'text-primary'
                                : 'text-foreground'
                            }`}>
                              {tradeType.label}
                            </span>
                          </div>
                          <p className="text-sm text-muted-foreground">{tradeType.description}</p>
                        </button>
                      ))}
                    </div>
                  </div>

                  {/* Integration */}
                  <div className="space-y-3">
                    <label className="block text-sm font-medium text-foreground">Broker Integration</label>
                    <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
                      {INTEGRATION_TYPES.map((integration) => (
                        <button
                          key={integration.value}
                          type="button"
                          onClick={() => setFormData({ ...formData, integration: integration.value })}
                          className={`p-4 rounded-lg border-2 text-center transition-all ${
                            formData.integration === integration.value
                              ? 'border-primary bg-primary/10 text-primary'
                              : 'border-border bg-muted/30 text-muted-foreground hover:border-muted-foreground/50 hover:text-foreground'
                          }`}
                        >
                          <div className="font-medium text-sm mb-1">{integration.label}</div>
                          <div className="text-xs text-muted-foreground">{integration.description}</div>
                        </button>
                      ))}
                    </div>
                    {formData.type === 'Live' && formData.integration === 'Default' && (
                      <div className="p-3 rounded-lg bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800">
                        <p className="text-sm text-amber-700 dark:text-amber-400 flex items-center gap-2">
                          <AlertCircle className="w-4 h-4" />
                          Select a broker integration for live trading
                        </p>
                      </div>
                    )}
                  </div>
                </div>
              )}

              {/* Position Section */}
              {activeSection === 'position' && (
                <div className="space-y-6">
                  <div>
                    <h2 className="text-lg font-semibold text-foreground mb-1">Position Sizing</h2>
                    <p className="text-sm text-muted-foreground">Configure how positions are sized and managed</p>
                  </div>
                  <PositionSettingsForm
                    value={formData.positionSettings}
                    onChange={(positionSettings) => setFormData({ ...formData, positionSettings })}
                  />
                </div>
              )}

              {/* Exit Section */}
              {activeSection === 'exit' && (
                <div className="space-y-6">
                  <div>
                    <h2 className="text-lg font-semibold text-foreground mb-1">Exit Rules</h2>
                    <p className="text-sm text-muted-foreground">Define when and how positions should be closed</p>
                  </div>
                  <ExitSettingsForm
                    value={formData.exitSettings}
                    onChange={(exitSettings) => setFormData({ ...formData, exitSettings })}
                  />
                </div>
              )}

              {/* Entry Section */}
              {activeSection === 'entry' && (
                <div className="space-y-6">
                  <div>
                    <h2 className="text-lg font-semibold text-foreground mb-1">Entry Conditions</h2>
                    <p className="text-sm text-muted-foreground">Define the criteria for entering trades</p>
                  </div>
                  <EntrySettingsForm
                    value={formData.entrySettings}
                    onChange={(entrySettings) => setFormData({ ...formData, entrySettings })}
                  />
                </div>
              )}
            </Card>

            {/* Bottom Actions (visible on all sections) */}
            <div className="mt-6 flex items-center justify-between">
              <div className="flex items-center gap-2">
                {activeSection !== 'general' && (
                  <Button
                    variant="outline"
                    onClick={() => {
                      const currentIndex = SECTIONS.findIndex(s => s.id === activeSection);
                      if (currentIndex > 0) {
                        setActiveSection(SECTIONS[currentIndex - 1].id);
                      }
                    }}
                  >
                    Previous
                  </Button>
                )}
              </div>
              <div className="flex items-center gap-3">
                {activeSection !== 'entry' && (
                  <Button
                    variant="outline"
                    onClick={() => {
                      const currentIndex = SECTIONS.findIndex(s => s.id === activeSection);
                      if (currentIndex < SECTIONS.length - 1) {
                        setActiveSection(SECTIONS[currentIndex + 1].id);
                      }
                    }}
                  >
                    Next Section
                  </Button>
                )}
                <Button
                  onClick={handleSubmit}
                  disabled={isLoading || !isValid}
                  className="bg-primary hover:bg-primary/90"
                >
                  {isLoading ? (
                    <>
                      <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                      Saving...
                    </>
                  ) : (
                    <>
                      <Save className="w-4 h-4 mr-2" />
                      {isEditMode ? 'Save Changes' : 'Create Strategy'}
                    </>
                  )}
                </Button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default StrategyEditorPage;

