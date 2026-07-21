import { useEffect, useRef, useState } from 'react';
import { useNavigate, useParams, useLocation, Link } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Strategy, Exit, IntegrationType, Timeframe, TradeType } from '../../types/strategy';
import { PositionSettingsForm } from '../../components/forms/strategy/PositionSettingsForm';
import { ExitSettingsForm } from '../../components/forms/strategy/ExitSettingsForm';
import { EntrySettingsForm } from '../../components/forms/strategy/EntrySettingsForm';
import { RailRow } from '../../components/backtest/BacktestReport';
import { Switch } from '../../components/ui/switch';
import { Button } from '../../components/ui/button';
import { Card } from '../../components/ui/card';
import { strategyApi } from '../../api/strategyApi';
import { toast } from '../../hooks/use-toast';
import { useUser } from '@clerk/react';
import { ArrowLeft, AlertCircle, Loader2, Save } from 'lucide-react';

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

/**
 * Execution tiers per plan 08: internal paper answers "does the strategy work",
 * Alpaca paper answers "does the integration work", Alpaca live is real money.
 * The Alpaca tiers unlock when the Phase 3 adapter ships.
 */
interface ExecutionTier {
  id: string;
  label: string;
  description: string;
  type: TradeType;
  integration: IntegrationType;
  available: boolean;
}

const EXECUTION_TIERS: ExecutionTier[] = [
  {
    id: 'internal-paper',
    label: 'Internal paper',
    description: 'Deterministic simulation. Fills at last minute close — same semantics as the backtester.',
    type: 'Paper',
    integration: 'Default',
    available: true,
  },
  {
    id: 'alpaca-paper',
    label: 'Alpaca paper',
    description: 'Dress rehearsal: the live integration path against Alpaca’s paper account.',
    type: 'Paper',
    integration: 'Default',
    available: false,
  },
  {
    id: 'alpaca-live',
    label: 'Alpaca live',
    description: 'Real money. Market orders with a broker-side disaster backstop.',
    type: 'Live',
    integration: 'Default',
    available: false,
  },
];

function mergeIntoDefaults(partial: Partial<Strategy>): Strategy {
  return {
    ...defaultFormData,
    ...partial,
    positionSettings: {
      ...defaultFormData.positionSettings,
      ...partial.positionSettings,
      model: {
        ...defaultFormData.positionSettings.model,
        ...partial.positionSettings?.model,
      },
    },
    exitSettings: {
      ...defaultFormData.exitSettings,
      ...partial.exitSettings,
    },
    entrySettings: {
      ...defaultFormData.entrySettings,
      ...partial.entrySettings,
      filters: partial.entrySettings?.filters || [],
    },
  };
}

const formatExit = (exit: Exit | undefined) => {
  if (!exit) return 'Not set';
  const amount = exit.type === 'percent' ? `${exit.value}%` : `$${exit.value}`;
  return `${exit.priceActionType} ${amount}`;
};

const formatTimeframe = (timeframe: Timeframe | undefined) => {
  if (!timeframe) return 'Not set';
  return `${timeframe.multiplier} ${timeframe.timespan}${timeframe.multiplier > 1 ? 's' : ''}`;
};

function SectionHeading({ index, label, title, hint }: { index: string; label: string; title: string; hint?: string }) {
  return (
    <div className="mb-4">
      <div className="mb-1 flex items-baseline gap-2 text-[11px] uppercase tracking-widest text-muted-foreground">
        <span className="font-mono">{index}</span>
        <span>{label}</span>
      </div>
      <h2 className="text-lg font-semibold tracking-tight">{title}</h2>
      {hint && <p className="mt-0.5 text-[13px] text-muted-foreground">{hint}</p>}
    </div>
  );
}

const StrategyEditorPage = () => {
  const navigate = useNavigate();
  const location = useLocation();
  const queryClient = useQueryClient();
  const { strategyId } = useParams<{ strategyId: string }>();

  const isEditMode = !!strategyId;
  const [formData, setFormData] = useState<Strategy>(defaultFormData);
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);
  const initializedFromNavState = useRef(false);
  const { isLoaded, isSignedIn } = useUser();

  // Redirect once Clerk has loaded and the user is definitely signed out
  useEffect(() => {
    if (isLoaded && !isSignedIn) {
      toast({
        title: 'Authentication Required',
        description: 'Please log in to create or edit strategies',
        variant: 'destructive',
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

  // Initialize form with existing strategy data or from navigation state (e.g. backtest handoff)
  useEffect(() => {
    if (existingStrategy) {
      setFormData(mergeIntoDefaults(existingStrategy));
      setHasUnsavedChanges(false);
    } else if (location.state?.initialData && !initializedFromNavState.current) {
      initializedFromNavState.current = true;
      setFormData(mergeIntoDefaults(location.state.initialData));
      setHasUnsavedChanges(true);
    }
  }, [existingStrategy, location.state]);

  const update = (patch: Partial<Strategy>) => {
    setFormData((prev) => ({ ...prev, ...patch }));
    setHasUnsavedChanges(true);
  };

  const createMutation = useMutation({
    mutationFn: strategyApi.createStrategy,
    onSuccess: (response) => {
      queryClient.invalidateQueries({ queryKey: ['myStrategies'] });
      toast({
        title: 'Strategy Created',
        description: `"${formData.name}" has been created successfully`,
      });
      navigate(`/optimus/strategy/${response.id}`);
    },
    onError: () => {
      toast({
        title: 'Error',
        description: 'Failed to create strategy. Please try again.',
        variant: 'destructive',
      });
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: string; data: Partial<Strategy> }) =>
      strategyApi.updateStrategy(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['strategy', strategyId] });
      queryClient.invalidateQueries({ queryKey: ['myStrategies'] });
      setHasUnsavedChanges(false);
      toast({
        title: 'Strategy Updated',
        description: `"${formData.name}" has been saved successfully`,
      });
    },
    onError: () => {
      toast({
        title: 'Error',
        description: 'Failed to update strategy. Please try again.',
        variant: 'destructive',
      });
    },
  });

  const filters = formData.entrySettings.filters;

  const handleSubmit = () => {
    if (!formData.name.trim()) {
      toast({
        title: 'Name required',
        description: 'Give the strategy a name before saving.',
        variant: 'destructive',
      });
      return;
    }

    // An active strategy with no filters would be evaluated against the whole
    // market by the scanner — never let one out the door.
    if (formData.state === 'Active' && filters.length === 0) {
      toast({
        title: 'No entry conditions',
        description: 'An active strategy needs at least one entry filter. Add one or set it to inactive.',
        variant: 'destructive',
      });
      return;
    }

    if (isEditMode && strategyId) {
      updateMutation.mutate({ id: strategyId, data: formData });
    } else {
      createMutation.mutate(formData);
    }
  };

  const isSaving = createMutation.isPending || updateMutation.isPending;
  const isValid = formData.name.trim().length > 0;

  const selectedTier =
    EXECUTION_TIERS.find((t) => t.available && t.type === formData.type && t.integration === formData.integration) ??
    EXECUTION_TIERS[0];

  const { positionSettings, exitSettings } = formData;

  if (isEditMode && isLoadingStrategy) {
    return (
      <div className="min-h-screen bg-background p-4 pt-20 text-foreground md:p-8 md:pt-8">
        <div className="mx-auto max-w-[1240px]">
          <Card className="p-8 text-center">
            <div className="mb-2 text-xs uppercase tracking-widest text-muted-foreground">Loading</div>
            <div className="text-base">Fetching strategy…</div>
          </Card>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-background p-4 pt-20 text-foreground md:p-8 md:pt-8">
      <div className="mx-auto max-w-[1240px]">
        {/* ---------- Masthead ---------- */}
        <header className="mb-6 flex flex-wrap items-end gap-4 border-b-2 border-foreground/80 pb-5">
          <div className="min-w-0 flex-1">
            <div className="mb-1.5 flex items-center gap-3">
              <Link
                to="/optimus/dashboard"
                className="inline-flex items-center gap-1 text-xs uppercase tracking-widest text-muted-foreground transition-colors hover:text-foreground"
              >
                <ArrowLeft className="h-3.5 w-3.5" />
                Strategies
              </Link>
              <span
                className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-[11px] font-semibold uppercase tracking-wide ${
                  formData.state === 'Active'
                    ? 'bg-green-500/10 text-green-600 dark:text-green-400'
                    : 'bg-muted text-muted-foreground'
                }`}
              >
                <span className="h-1.5 w-1.5 rounded-full bg-current" />
                {isEditMode ? formData.state : 'Draft'}
              </span>
              {isEditMode && strategyId && (
                <span className="font-mono text-[11px] text-muted-foreground">{strategyId.slice(0, 8)}</span>
              )}
              {hasUnsavedChanges && (
                <span className="flex items-center gap-1 text-[11px] uppercase tracking-widest text-amber-600 dark:text-amber-400">
                  <AlertCircle className="h-3 w-3" />
                  Unsaved
                </span>
              )}
            </div>
            <input
              type="text"
              value={formData.name}
              onChange={(e) => update({ name: e.target.value })}
              placeholder="Untitled strategy"
              autoFocus={!isEditMode}
              className="w-full max-w-2xl border-b border-transparent bg-transparent text-2xl font-semibold tracking-tight outline-none transition-colors placeholder:text-muted-foreground/50 focus:border-border md:text-3xl"
            />
            <p className="mt-1 text-[13px] text-muted-foreground tabular-nums">
              {selectedTier.label} · {filters.length} entry filter{filters.length !== 1 && 's'} · $
              {positionSettings.startingBalance.toLocaleString()} starting balance
            </p>
          </div>
          <div className="ml-auto flex gap-2 pb-1">
            <Button size="sm" onClick={handleSubmit} disabled={isSaving || !isValid}>
              {isSaving ? (
                <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />
              ) : (
                <Save className="mr-1.5 h-4 w-4" />
              )}
              {isEditMode ? 'Save changes' : 'Create strategy'}
            </Button>
          </div>
        </header>

        <div className="grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,1fr)_320px]">
          {/* ---------- Main column ---------- */}
          <div className="flex min-w-0 flex-col gap-6">
            <Card className="p-5">
              <SectionHeading
                index="01"
                label="Entry"
                title="Entry conditions"
                hint="Evaluated against live market data every 15 seconds while the market is open."
              />
              <EntrySettingsForm
                value={formData.entrySettings}
                onChange={(entrySettings) => update({ entrySettings })}
              />
            </Card>

            <Card className="p-5">
              <SectionHeading
                index="02"
                label="Exit"
                title="Exit rules"
                hint="Checked every few seconds per open position. Same-bar ties resolve to the stop, matching the backtester."
              />
              <ExitSettingsForm
                value={formData.exitSettings}
                onChange={(exitSettings) => update({ exitSettings })}
              />
            </Card>

            <Card className="p-5">
              <SectionHeading
                index="03"
                label="Position"
                title="Position sizing"
                hint="Capital, sizing model, concurrency, and re-entry cooldown."
              />
              <PositionSettingsForm
                value={formData.positionSettings}
                onChange={(positionSettings) => update({ positionSettings })}
              />
            </Card>

            <Card className="p-5">
              <SectionHeading
                index="04"
                label="Execution"
                title="Where this strategy runs"
                hint="Prove it on internal paper first — same fill semantics as the backtest you just ran."
              />

              <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
                {EXECUTION_TIERS.map((tier) => {
                  const isSelected = tier.id === selectedTier.id;
                  return (
                    <button
                      key={tier.id}
                      type="button"
                      disabled={!tier.available}
                      onClick={() => update({ type: tier.type, integration: tier.integration })}
                      className={`relative rounded-lg border p-4 text-left transition-colors ${
                        isSelected
                          ? 'border-primary bg-primary/5'
                          : tier.available
                            ? 'border-border hover:border-muted-foreground/50'
                            : 'cursor-not-allowed border-border/60 opacity-55'
                      }`}
                    >
                      <div className="mb-1 flex items-center justify-between gap-2">
                        <span className={`text-sm font-semibold ${isSelected ? 'text-primary' : 'text-foreground'}`}>
                          {tier.label}
                        </span>
                        {!tier.available && (
                          <span className="rounded-full border border-border px-2 py-0.5 text-[10px] uppercase tracking-widest text-muted-foreground">
                            Soon
                          </span>
                        )}
                      </div>
                      <p className="text-xs leading-relaxed text-muted-foreground">{tier.description}</p>
                    </button>
                  );
                })}
              </div>

              <div className="mt-5 grid grid-cols-1 gap-3 md:grid-cols-2">
                <div className="flex items-center justify-between gap-4 rounded-lg border border-border p-4">
                  <div>
                    <div className="flex items-center gap-2">
                      <span
                        className={`h-2 w-2 rounded-full ${
                          formData.state === 'Active' ? 'animate-pulse bg-green-500' : 'bg-muted-foreground/50'
                        }`}
                      />
                      <span className="text-sm font-medium">
                        {formData.state === 'Active' ? 'Active' : 'Inactive'}
                      </span>
                    </div>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {formData.state === 'Active'
                        ? 'Scanned every 15s during market hours; signals execute within seconds.'
                        : 'Saved but not scanned. Activate when you’re ready.'}
                    </p>
                  </div>
                  <Switch
                    checked={formData.state === 'Active'}
                    onCheckedChange={(checked) => update({ state: checked ? 'Active' : 'Inactive' })}
                  />
                </div>

                <div className="flex items-center justify-between gap-4 rounded-lg border border-border p-4">
                  <div>
                    <span className="text-sm font-medium">{formData.visibility}</span>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {formData.visibility === 'Public'
                        ? 'Others can view this strategy and its results.'
                        : 'Only you can see this strategy.'}
                    </p>
                  </div>
                  <Switch
                    checked={formData.visibility === 'Public'}
                    onCheckedChange={(checked) => update({ visibility: checked ? 'Public' : 'Private' })}
                  />
                </div>
              </div>
            </Card>
          </div>

          {/* ---------- Config rail (mirrors the backtest report rail) ---------- */}
          <aside className="flex flex-col gap-4 self-start lg:sticky lg:top-4">
            <Card className="p-4">
              <h3 className="mb-2 text-[11px] uppercase tracking-widest text-muted-foreground">
                Entry filters
              </h3>
              {filters.length > 0 ? (
                <div className="flex flex-col gap-1.5">
                  {filters.map((filter, index) => (
                    <code
                      key={`${filter}-${index}`}
                      className="rounded-md border border-border/60 bg-muted/50 px-2.5 py-1.5 font-mono text-xs"
                    >
                      {filter}
                    </code>
                  ))}
                </div>
              ) : (
                <p className="text-[13px] text-muted-foreground">
                  None yet — an active strategy needs at least one.
                </p>
              )}
            </Card>

            <Card className="p-4">
              <h3 className="mb-1 text-[11px] uppercase tracking-widest text-muted-foreground">
                Position
              </h3>
              <RailRow
                label="Starting balance"
                value={`$${positionSettings.startingBalance.toLocaleString()}`}
              />
              <RailRow
                label="Position size"
                value={
                  positionSettings.model.type === 'Percentage'
                    ? `${positionSettings.model.size}% of balance`
                    : `$${positionSettings.model.size.toLocaleString()} fixed`
                }
              />
              <RailRow label="Max concurrent" value={String(positionSettings.maxConcurrentPositions)} />
              <RailRow
                label="Simultaneous entries"
                value={positionSettings.allowSimultaneous ? 'Allowed' : 'Not allowed'}
              />
              <RailRow label="Cooldown" value={formatTimeframe(positionSettings.cooldown)} />
            </Card>

            <Card className="p-4">
              <h3 className="mb-1 text-[11px] uppercase tracking-widest text-muted-foreground">
                Exits
              </h3>
              <RailRow
                label="Stop loss"
                value={formatExit(exitSettings.stopLoss)}
                valueColor={exitSettings.stopLoss ? 'var(--chart-loss)' : undefined}
              />
              <RailRow
                label="Take profit"
                value={formatExit(exitSettings.takeProfit)}
                valueColor={exitSettings.takeProfit ? 'var(--chart-gain)' : undefined}
              />
              <RailRow label="Timed exit" value={formatTimeframe(exitSettings.timedExit?.timeframe)} />
              <RailRow
                label="Overnight"
                value={exitSettings.timedExit ? (exitSettings.timedExit.avoidOvernight ? 'Avoided' : 'Allowed') : '—'}
              />
            </Card>

            <Card className="p-4">
              <h3 className="mb-1 text-[11px] uppercase tracking-widest text-muted-foreground">
                Execution
              </h3>
              <RailRow label="Tier" value={selectedTier.label} />
              <RailRow label="State" value={formData.state} />
              <RailRow label="Visibility" value={formData.visibility} />
            </Card>
          </aside>
        </div>
      </div>
    </div>
  );
};

export default StrategyEditorPage;
