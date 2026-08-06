import { useEffect, useMemo, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { ArrowLeft, Loader2, Play } from 'lucide-react';
import type { BacktestRequest } from '../types/backtest';
import type { Exit, ExitSettings, PositionSettings, Timeframe } from '../types/strategy';
import { backtestApi } from '../api/backtestApi';
import { Clock } from '../components/clock/Clock';
import { MarketStatus } from '../components/market';
import { ApiStatus } from '../components/status';
import { Card } from '../components/ui/card';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';
import { RailRow } from '../components/backtest/BacktestReport';
import { EntrySettingsForm } from '../components/forms/strategy/EntrySettingsForm';
import { ExitSettingsForm, defaultExitSettings } from '../components/forms/strategy/ExitSettingsForm';
import { PositionSettingsForm } from '../components/forms/strategy/PositionSettingsForm';
import { toast } from '../hooks/use-toast';

const formatDateInput = (date: Date) => date.toISOString().split('T')[0];

const defaultPositionSettings: PositionSettings = {
  startingBalance: 10000,
  maxConcurrentPositions: 3,
  allowSimultaneous: false,
  model: {
    type: 'Fixed',
    size: 1000,
  },
};

interface BacktestFormData {
  positionSettings: PositionSettings;
  exitSettings: ExitSettings;
  filters: string[];
}

const defaultFormData: BacktestFormData = {
  positionSettings: defaultPositionSettings,
  exitSettings: defaultExitSettings,
  filters: [],
};

/**
 * Merges a prefill request (e.g. "copy backtest") into the form defaults. Exits are
 * mandatory now, but copied records may predate that rule — fill gaps with defaults.
 */
function fromRequest(request: BacktestRequest): BacktestFormData {
  return {
    positionSettings: {
      ...defaultPositionSettings,
      ...request.positionSettings,
      model: { ...defaultPositionSettings.model, ...request.positionSettings?.model },
    },
    exitSettings: {
      stopLoss: request.exitSettings?.stopLoss ?? defaultExitSettings.stopLoss,
      takeProfit: request.exitSettings?.takeProfit ?? defaultExitSettings.takeProfit,
      timedExit: request.exitSettings?.timedExit?.timeframe
        ? request.exitSettings.timedExit
        : defaultExitSettings.timedExit,
    },
    filters: request.entrySettings?.filters ?? [],
  };
}

const formatExit = (exit: Exit) =>
  exit.type === 'percent' ? `${exit.value}%` : `$${exit.value}`;

const formatTimeframe = (timeframe: Timeframe | undefined) => {
  if (!timeframe) return 'Off';
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

export function BacktestCreatePage() {
  const location = useLocation();
  const backtestDefaults = (location.state as { backtestDefaults?: BacktestRequest } | null)?.backtestDefaults;
  const navigate = useNavigate();
  const [isCreating, setIsCreating] = useState(false);
  const [lastPrefillSignature, setLastPrefillSignature] = useState<string | null>(null);

  const defaultEnd = useMemo(() => new Date(), []);
  const defaultStart = useMemo(
    () => new Date(defaultEnd.getTime() - 7 * 24 * 60 * 60 * 1000),
    [defaultEnd]
  );

  const [startDate, setStartDate] = useState(formatDateInput(defaultStart));
  const [endDate, setEndDate] = useState(formatDateInput(defaultEnd));
  const [formData, setFormData] = useState<BacktestFormData>(defaultFormData);

  const update = (patch: Partial<BacktestFormData>) =>
    setFormData((prev) => ({ ...prev, ...patch }));

  useEffect(() => {
    if (!backtestDefaults) return;

    const signature = JSON.stringify(backtestDefaults);
    if (signature === lastPrefillSignature) return;

    if (backtestDefaults.start) setStartDate(backtestDefaults.start.slice(0, 10));
    if (backtestDefaults.end) setEndDate(backtestDefaults.end.slice(0, 10));
    setFormData(fromRequest(backtestDefaults));
    setLastPrefillSignature(signature);
  }, [lastPrefillSignature, backtestDefaults]);

  const { positionSettings, exitSettings, filters } = formData;

  const handleCreateBacktest = async () => {
    if (!startDate || !endDate) {
      toast({
        title: 'Date range missing',
        description: 'Please provide both start and end dates for the backtest.',
        variant: 'destructive',
      });
      return;
    }

    if (new Date(startDate) > new Date(endDate)) {
      toast({
        title: 'Invalid date range',
        description: 'Start date must be on or before the end date.',
        variant: 'destructive',
      });
      return;
    }

    if (filters.length === 0) {
      toast({
        title: 'No entry conditions',
        description: 'Add at least one entry filter before running a backtest.',
        variant: 'destructive',
      });
      return;
    }

    const payload: BacktestRequest = {
      start: startDate,
      end: endDate,
      positionSettings,
      entrySettings: { filters },
      exitSettings,
    };

    try {
      setIsCreating(true);

      const result = await backtestApi.createBacktest(payload);

      toast({
        title: 'Backtest started',
        description: 'We will notify you when results are ready.',
      });

      if (result.id) {
        navigate(`/backtest/${result.id}`);
      } else {
        navigate('/backtest');
      }
    } catch (e) {
      console.error('Failed to create backtest:', e);
      toast({
        title: 'Backtest failed',
        description: e instanceof Error ? e.message : 'Unable to start the backtest.',
        variant: 'destructive',
      });
    } finally {
      setIsCreating(false);
    }
  };

  return (
    <div className="min-h-screen bg-background p-4 pt-20 text-foreground md:p-8 md:pt-8">
      <div className="mx-auto max-w-[1240px]">
        {/* ---------- Masthead ---------- */}
        <header className="mb-6 flex flex-wrap items-end gap-4 border-b-2 border-foreground/80 pb-5">
          <div className="min-w-0 flex-1">
            <div className="mb-1.5 flex items-center gap-3">
              <button
                type="button"
                onClick={() => navigate('/backtest')}
                className="inline-flex items-center gap-1 text-xs uppercase tracking-widest text-muted-foreground transition-colors hover:text-foreground"
              >
                <ArrowLeft className="h-3.5 w-3.5" />
                Results
              </button>
              <div className="flex items-center gap-2">
                <Clock />
                <MarketStatus />
                <ApiStatus />
              </div>
            </div>
            <h1 className="text-2xl font-semibold tracking-tight md:text-3xl">Create Backtest</h1>
            <p className="mt-1 text-[13px] text-muted-foreground tabular-nums">
              {filters.length} entry filter{filters.length !== 1 && 's'} · $
              {positionSettings.startingBalance.toLocaleString()} starting balance
            </p>
          </div>
          <div className="ml-auto flex flex-wrap items-center gap-3 pb-1">
            <div className="flex items-center gap-2 rounded-lg border border-input bg-card px-3 py-1.5">
              <span className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">From</span>
              <Input
                type="date"
                value={startDate}
                onChange={(event) => setStartDate(event.target.value)}
                disabled={isCreating}
                className="bg-transparent border-0 text-foreground text-xs tabular-nums h-6 w-[120px] p-0 focus-visible:ring-0"
              />
              <span className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">To</span>
              <Input
                type="date"
                value={endDate}
                onChange={(event) => setEndDate(event.target.value)}
                disabled={isCreating}
                className="bg-transparent border-0 text-foreground text-xs tabular-nums h-6 w-[120px] p-0 focus-visible:ring-0"
              />
            </div>
            <Button size="sm" onClick={handleCreateBacktest} disabled={isCreating}>
              {isCreating ? (
                <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />
              ) : (
                <Play className="mr-1.5 h-4 w-4" />
              )}
              {isCreating ? 'Submitting…' : 'Start Backtest'}
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
                hint="Signals fire on every minute bar in the range where all filters pass."
              />
              <EntrySettingsForm
                value={{ filters }}
                onChange={(entrySettings) => update({ filters: entrySettings.filters })}
              />
            </Card>

            <Card className="p-5">
              <SectionHeading
                index="02"
                label="Exit"
                title="Exit rules"
                hint="Close-based fills, same semantics as live. Same-bar ties resolve to the stop."
              />
              <ExitSettingsForm
                value={exitSettings}
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
                value={positionSettings}
                onChange={(positionSettings) => update({ positionSettings })}
              />
            </Card>
          </div>

          {/* ---------- Config rail (mirrors the strategy editor rail) ---------- */}
          <aside className="flex flex-col gap-4 self-start lg:sticky lg:top-4">
            <Card className="p-4">
              <h3 className="mb-1 text-[11px] uppercase tracking-widest text-muted-foreground">
                Range
              </h3>
              <RailRow label="From" value={startDate || '—'} />
              <RailRow label="To" value={endDate || '—'} />
            </Card>

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
                  None yet — a backtest needs at least one.
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
                valueColor="var(--chart-loss)"
              />
              <RailRow
                label="Take profit"
                value={formatExit(exitSettings.takeProfit)}
                valueColor="var(--chart-gain)"
              />
              <RailRow label="Timed exit" value={formatTimeframe(exitSettings.timedExit.timeframe)} />
              <RailRow
                label="Overnight"
                value={exitSettings.timedExit.avoidOvernight ? 'Avoided' : 'Allowed'}
              />
            </Card>
          </aside>
        </div>
      </div>
    </div>
  );
}
