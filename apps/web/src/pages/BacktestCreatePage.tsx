import { useCallback, useEffect, useMemo, useState, useRef } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import {
  BacktestRequest,
  BacktestExitSettings,
  BacktestPositionSettings,
} from '../types/backtest';
import { backtestApi } from '../api/backtestApi';
import { Clock } from '../components/clock/Clock';
import { MarketStatus } from '../components/market';
import { ApiStatus } from '../components/status';
import { Card } from '../components/ui/card';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';
import { Switch } from '../components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../components/ui/tabs';
import { FilterComposer, type FilterComposerRef } from '../components/filters/FilterComposer';
import { FilterList } from '../components/filters/FilterList';
import { toast } from '../hooks/use-toast';
import type { FilterItem } from '../types/filters';

const createFilter = (expression: string): FilterItem => ({
  id: crypto.randomUUID(),
  enabled: true,
  expression,
});

const formatDateInput = (date: Date) => date.toISOString().split('T')[0];

const TIMESPAN_OPTIONS = ['minute', 'hour', 'day', 'week', 'month', 'year'] as const;
const PRICE_ACTION_OPTIONS = ['open', 'high', 'low', 'close'] as const;
const CANDLE_TYPE_OPTIONS = ['EntryCandle', 'PreviousCandle'] as const;
const MODEL_TYPE_OPTIONS = ['Fixed', 'PercentOfEquity'] as const;

export function BacktestCreatePage() {
  const location = useLocation<{ backtestDefaults?: BacktestRequest }>();
  const navigate = useNavigate();
  const [error, setError] = useState('');
  const [isCreatingBacktest, setIsCreatingBacktest] = useState(false);
  const [lastPrefillSignature, setLastPrefillSignature] = useState<string | null>(null);

  const defaultEnd = useMemo(() => new Date(), []);
  const defaultStart = useMemo(
    () => new Date(defaultEnd.getTime() - 7 * 24 * 60 * 60 * 1000),
    [defaultEnd]
  );

  const [startDate, setStartDate] = useState(formatDateInput(defaultStart));
  const [endDate, setEndDate] = useState(formatDateInput(defaultEnd));
  const [filters, setFilters] = useState<FilterItem[]>([]);

  const [startingBalance, setStartingBalance] = useState<number>(10000);
  const [allowSimultaneous, setAllowSimultaneous] = useState(false);
  const [maxConcurrentPositions, setMaxConcurrentPositions] = useState<number>(3);
  const [modelType, setModelType] = useState<(typeof MODEL_TYPE_OPTIONS)[number]>('Fixed');
  const [modelSize, setModelSize] = useState<number>(1000);
  const [cooldownEnabled, setCooldownEnabled] = useState(false);
  const [cooldownMultiplier, setCooldownMultiplier] = useState<number>(1);
  const [cooldownTimespan, setCooldownTimespan] = useState<(typeof TIMESPAN_OPTIONS)[number]>('minute');

  const [takeProfitEnabled, setTakeProfitEnabled] = useState(true);
  const [takeProfitValue, setTakeProfitValue] = useState<number>(10);
  const [takeProfitValueInput, setTakeProfitValueInput] = useState<string>('10');
  const [takeProfitType, setTakeProfitType] = useState<'percent' | 'value'>('percent');
  const [takeProfitCandleType, setTakeProfitCandleType] = useState<(typeof CANDLE_TYPE_OPTIONS)[number]>('PreviousCandle');
  const [takeProfitPriceActionType, setTakeProfitPriceActionType] =
    useState<(typeof PRICE_ACTION_OPTIONS)[number]>('close');

  const [stopLossEnabled, setStopLossEnabled] = useState(true);
  const [stopLossValue, setStopLossValue] = useState<number>(-10);
  const [stopLossValueInput, setStopLossValueInput] = useState<string>('-10');
  const [stopLossType, setStopLossType] = useState<'percent' | 'value'>('percent');
  const [stopLossCandleType, setStopLossCandleType] = useState<(typeof CANDLE_TYPE_OPTIONS)[number]>('PreviousCandle');
  const [stopLossPriceActionType, setStopLossPriceActionType] =
    useState<(typeof PRICE_ACTION_OPTIONS)[number]>('close');

  const [timedExitEnabled, setTimedExitEnabled] = useState(true);
  const [timedExitMultiplier, setTimedExitMultiplier] = useState<number>(5);
  const [timedExitTimespan, setTimedExitTimespan] = useState<(typeof TIMESPAN_OPTIONS)[number]>('minute');

  const filterComposerRef = useRef<FilterComposerRef>(null);

  const hydrateFromRequest = useCallback(
    (request: BacktestRequest) => {
      if (request.start) {
        setStartDate(request.start.slice(0, 10));
      }
      if (request.end) {
        setEndDate(request.end.slice(0, 10));
      }

      const providedFilters = request.EntrySettings?.Filters;
      if (Array.isArray(providedFilters)) {
        setFilters(providedFilters.map(expression => createFilter(expression)));
      }

      if (request.PositionSettings) {
        setStartingBalance(request.PositionSettings.StartingBalance ?? 10000);
        setAllowSimultaneous(!!request.PositionSettings.AllowSimultaneous);
        setMaxConcurrentPositions(request.PositionSettings.MaxConcurrentPositions ?? 3);
        setModelType(
          (request.PositionSettings.Model?.Type as (typeof MODEL_TYPE_OPTIONS)[number]) ?? 'Fixed'
        );
        setModelSize(request.PositionSettings.Model?.Size ?? 1000);

        const cooldown = request.PositionSettings.Cooldown;
        setCooldownEnabled(!!cooldown);
        if (cooldown) {
          setCooldownMultiplier(cooldown.Multiplier ?? 1);
          setCooldownTimespan((cooldown.Timespan as (typeof TIMESPAN_OPTIONS)[number]) ?? 'minute');
        }
      }

      if (request.ExitSettings) {
        const { TakeProfit, StopLoss, TimedExit } = request.ExitSettings;

        setTakeProfitEnabled(!!TakeProfit);
        if (TakeProfit) {
          const tpValue = TakeProfit.Value ?? 10;
          setTakeProfitValue(tpValue);
          setTakeProfitValueInput(tpValue.toString());
          setTakeProfitType((TakeProfit.Type as 'percent' | 'value') ?? 'percent');
          setTakeProfitCandleType(
            (TakeProfit.CandleType as (typeof CANDLE_TYPE_OPTIONS)[number]) ?? 'PreviousCandle'
          );
          setTakeProfitPriceActionType(
            (TakeProfit.PriceActionType as (typeof PRICE_ACTION_OPTIONS)[number]) ?? 'close'
          );
        }

        setStopLossEnabled(!!StopLoss);
        if (StopLoss) {
          const slValue = StopLoss.Value ?? -10;
          setStopLossValue(slValue);
          setStopLossValueInput(slValue.toString());
          setStopLossType((StopLoss.Type as 'percent' | 'value') ?? 'percent');
          setStopLossCandleType(
            (StopLoss.CandleType as (typeof CANDLE_TYPE_OPTIONS)[number]) ?? 'PreviousCandle'
          );
          setStopLossPriceActionType(
            (StopLoss.PriceActionType as (typeof PRICE_ACTION_OPTIONS)[number]) ?? 'close'
          );
        }

        setTimedExitEnabled(!!TimedExit?.Timeframe);
        if (TimedExit?.Timeframe) {
          setTimedExitMultiplier(TimedExit.Timeframe.Multiplier ?? 5);
          setTimedExitTimespan(
            (TimedExit.Timeframe.Timespan as (typeof TIMESPAN_OPTIONS)[number]) ?? 'minute'
          );
        }
      }
    },
    []
  );

  useEffect(() => {
    const defaults = location.state?.backtestDefaults;
    if (!defaults) return;

    const signature = JSON.stringify(defaults);
    if (signature === lastPrefillSignature) return;

    hydrateFromRequest(defaults);
    setLastPrefillSignature(signature);
  }, [hydrateFromRequest, lastPrefillSignature, location.state?.backtestDefaults]);

  const handleAddFilter = (expression: string) => {
    if (!expression.trim()) return;
    setFilters(prev => [createFilter(expression.trim()), ...prev]);
  };

  const handleToggleFilter = (id: string) => {
    setFilters(prev =>
      prev.map(filter =>
        filter.id === id ? { ...filter, enabled: !filter.enabled } : filter
      )
    );
  };

  const handleRemoveFilter = (id: string) => {
    setFilters(prev => prev.filter(filter => filter.id !== id));
  };

  const handleClearFilters = () => setFilters([]);

  const handleEditFilter = (id: string) => {
    const filter = filters.find(f => f.id === id);
    if (filter && filterComposerRef.current) {
      filterComposerRef.current.setExpression(filter.expression);
      handleRemoveFilter(id);
    }
  };

  const activeFilters = useMemo(
    () =>
      filters
        .filter(filter => filter.enabled)
        .map(filter => filter.expression.trim())
        .filter(Boolean),
    [filters]
  );

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

    if (activeFilters.length === 0) {
      toast({
        title: 'No filters selected',
        description: 'Add at least one enabled filter in Entry Settings before running a backtest.',
        variant: 'destructive',
      });
      return;
    }

    const positionSettings: BacktestPositionSettings = {
      StartingBalance: Number.isFinite(startingBalance) ? startingBalance : 0,
      AllowSimultaneous: allowSimultaneous,
      MaxConcurrentPositions: Number.isFinite(maxConcurrentPositions) ? maxConcurrentPositions : 0,
      Model: {
        Type: modelType,
        Size: Number.isFinite(modelSize) ? modelSize : 0,
      },
    };

    if (cooldownEnabled) {
      positionSettings.Cooldown = {
        Multiplier: Number.isFinite(cooldownMultiplier) ? cooldownMultiplier : 1,
        Timespan: cooldownTimespan,
      };
    }

    const exitSettings: BacktestExitSettings = {};

    if (takeProfitEnabled) {
      exitSettings.TakeProfit = {
        CandleType: takeProfitCandleType,
        Type: takeProfitType,
        Value: Number.isFinite(takeProfitValue) ? takeProfitValue : 0,
        PriceActionType: takeProfitPriceActionType,
      };
    }

    if (stopLossEnabled) {
      exitSettings.StopLoss = {
        CandleType: stopLossCandleType,
        Type: stopLossType,
        Value: Number.isFinite(stopLossValue) ? stopLossValue : 0,
        PriceActionType: stopLossPriceActionType,
      };
    }

    if (timedExitEnabled) {
      exitSettings.TimedExit = {
        Timeframe: {
          Multiplier: Number.isFinite(timedExitMultiplier) ? timedExitMultiplier : 1,
          Timespan: timedExitTimespan,
        },
      };
    }

    const payload: BacktestRequest = {
      start: startDate,
      end: endDate,
      PositionSettings: positionSettings,
      EntrySettings: {
        Filters: activeFilters,
      },
      ExitSettings: exitSettings,
    };

    try {
      setIsCreatingBacktest(true);
      setError('');
      
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
      setError('Failed to create backtest. Please try again.');
      toast({
        title: 'Backtest failed',
        description: e instanceof Error ? e.message : 'Unable to start the backtest.',
        variant: 'destructive',
      });
    } finally {
      setIsCreatingBacktest(false);
    }
  };

  return (
    <div className="min-h-screen bg-background p-4 md:p-6 pt-20 md:pt-6">
      <div className="max-w-7xl mx-auto space-y-4">
        {/* Compact Header with Date Range */}
        <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4 border-b border-border pb-4">
          <div className="flex items-center gap-3">
            <div className="flex items-center gap-2">
              <Clock />
              <MarketStatus />
              <ApiStatus />
            </div>
            <div>
              <h1 className="text-lg font-mono font-bold uppercase tracking-wider text-foreground"># Create Backtest</h1>
              <p className="text-[10px] font-mono text-muted-foreground">{'>> '}Configure and launch a new strategy backtest</p>
            </div>
          </div>
          
          <div className="flex items-center gap-3 flex-wrap">
            <div className="flex items-center gap-2 bg-muted/30 dark:bg-muted/50 border border-border rounded-md px-3 py-1.5">
              <span className="text-[10px] font-mono uppercase text-muted-foreground">From</span>
              <Input
                type="date"
                value={startDate}
                onChange={event => setStartDate(event.target.value)}
                disabled={isCreatingBacktest}
                className="bg-transparent border-0 text-foreground font-mono text-xs h-6 w-[120px] p-0 focus-visible:ring-0"
              />
              <span className="text-[10px] font-mono uppercase text-muted-foreground">To</span>
              <Input
                type="date"
                value={endDate}
                onChange={event => setEndDate(event.target.value)}
                disabled={isCreatingBacktest}
                className="bg-transparent border-0 text-foreground font-mono text-xs h-6 w-[120px] p-0 focus-visible:ring-0"
              />
            </div>
            
            <Button
              variant="outline"
              onClick={() => navigate('/backtest')}
              className="font-mono text-xs uppercase h-9"
            >
              ← Results
            </Button>
            
            <Button
              onClick={handleCreateBacktest}
              disabled={isCreatingBacktest}
              className="bg-green-100 dark:bg-green-950 border border-green-300 dark:border-green-700 text-green-700 dark:text-green-400 hover:bg-green-200 dark:hover:bg-green-900 hover:border-green-400 dark:hover:border-green-500 font-mono text-xs uppercase px-4 h-9 transition-all disabled:opacity-50"
            >
              {isCreatingBacktest ? 'Submitting...' : 'Start Backtest'}
            </Button>
          </div>
        </div>

        {error && (
          <div className="bg-destructive/10 dark:bg-red-950/50 border border-destructive dark:border-red-700 text-destructive dark:text-red-400 px-4 py-2 relative font-mono text-sm rounded">
            <span className="text-destructive dark:text-red-500">ERROR:</span> {error}
            <button
              onClick={() => setError('')}
              className="absolute top-1 right-2 text-destructive dark:text-red-400 hover:text-destructive/80 dark:hover:text-red-300 font-bold"
            >
              ×
            </button>
          </div>
        )}

        {/* Main Content - Two Column Layout */}
        <div className="grid gap-4 lg:grid-cols-2">
          {/* Left Column - Filter Composer & List */}
          <div className="space-y-4">
            <Card className="p-4 bg-card border border-border">
              <div className="text-xs font-mono uppercase tracking-wider text-primary dark:text-cyan-400 mb-3"># Filters</div>
              <FilterComposer 
                ref={filterComposerRef}
                onAddFilter={handleAddFilter} 
                disabled={isCreatingBacktest} 
              />
            </Card>
            
            <Card className="p-4 bg-card border border-border">
              <div className="flex items-center justify-between mb-3">
                <div className="text-xs font-mono uppercase tracking-wider text-muted-foreground">
                  :: Entry Filters ({activeFilters.length} active)
                </div>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={handleClearFilters}
                  disabled={filters.length === 0 || isCreatingBacktest}
                  className="bg-background border-border text-muted-foreground hover:border-destructive hover:text-destructive font-mono text-[10px] uppercase px-2 py-0.5 h-6 transition-all disabled:opacity-50"
                >
                  Clear All
                </Button>
              </div>
              <FilterList
                filters={filters}
                onToggle={handleToggleFilter}
                onRemove={handleRemoveFilter}
                onEdit={handleEditFilter}
                disabled={isCreatingBacktest}
              />
            </Card>
          </div>

          {/* Right Column - Settings with Tabs */}
          <Card className="p-4 bg-card border border-border">
            <Tabs defaultValue="position" className="w-full">
              <TabsList className="w-full bg-muted/50 border border-border mb-4">
                <TabsTrigger value="position" className="flex-1 font-mono text-xs uppercase data-[state=active]:bg-primary/10 data-[state=active]:text-primary">
                  Position
                </TabsTrigger>
                <TabsTrigger value="exit" className="flex-1 font-mono text-xs uppercase data-[state=active]:bg-primary/10 data-[state=active]:text-primary">
                  Exit Rules
                </TabsTrigger>
              </TabsList>

              <TabsContent value="position" className="space-y-4 mt-0">
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-1">
                    <label className="text-[10px] font-mono uppercase text-muted-foreground">Starting Balance</label>
                    <Input
                      type="text"
                      value={startingBalance}
                      onChange={event => {
                        const val = event.target.value;
                        if (val === '' || (!isNaN(Number(val)) && Number(val) >= 0)) {
                          setStartingBalance(val === '' ? 0 : Number(val));
                        }
                      }}
                      disabled={isCreatingBacktest}
                      className="bg-background border border-border text-foreground font-mono text-xs h-8"
                    />
                  </div>
                  <div className="space-y-1">
                    <label className="text-[10px] font-mono uppercase text-muted-foreground">Max Concurrent</label>
                    <Input
                      type="text"
                      value={maxConcurrentPositions}
                      onChange={event => {
                        const val = event.target.value;
                        if (val === '' || (!isNaN(Number(val)) && Number(val) >= 1)) {
                          setMaxConcurrentPositions(val === '' ? 1 : Number(val));
                        }
                      }}
                      disabled={isCreatingBacktest}
                      className="bg-background border border-border text-foreground font-mono text-xs h-8"
                    />
                  </div>
                  <div className="space-y-1">
                    <label className="text-[10px] font-mono uppercase text-muted-foreground">Model Type</label>
                    <select
                      value={modelType}
                      onChange={event =>
                        setModelType(event.target.value as (typeof MODEL_TYPE_OPTIONS)[number])
                      }
                      disabled={isCreatingBacktest}
                      className="w-full bg-background border border-border text-foreground font-mono text-xs uppercase px-2 py-1 h-8 rounded-md"
                    >
                      {MODEL_TYPE_OPTIONS.map(option => (
                        <option key={option} value={option}>
                          {option}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="space-y-1">
                    <label className="text-[10px] font-mono uppercase text-muted-foreground">Model Size</label>
                    <Input
                      type="text"
                      value={modelSize}
                      onChange={event => {
                        const val = event.target.value;
                        if (val === '' || (!isNaN(Number(val)) && Number(val) >= 0)) {
                          setModelSize(val === '' ? 0 : Number(val));
                        }
                      }}
                      disabled={isCreatingBacktest}
                      className="bg-background border border-border text-foreground font-mono text-xs h-8"
                    />
                  </div>
                </div>

                <div className="border-t border-border pt-3 space-y-3">
                  <div className="flex items-center justify-between">
                    <div>
                      <div className="text-[10px] font-mono uppercase text-muted-foreground">Cooldown</div>
                      <div className="text-[10px] font-mono text-muted-foreground/70">Wait before new positions</div>
                    </div>
                    <Switch
                      checked={cooldownEnabled}
                      onCheckedChange={setCooldownEnabled}
                      disabled={isCreatingBacktest}
                    />
                  </div>
                  {cooldownEnabled && (
                    <div className="grid grid-cols-2 gap-3 pl-2 border-l-2 border-primary/30">
                      <div className="space-y-1">
                        <label className="text-[10px] font-mono uppercase text-muted-foreground">Multiplier</label>
                        <Input
                          type="text"
                          value={cooldownMultiplier}
                          onChange={event => {
                            const val = event.target.value;
                            if (val === '' || (!isNaN(Number(val)) && Number(val) >= 1)) {
                              setCooldownMultiplier(val === '' ? 1 : Number(val));
                            }
                          }}
                          disabled={isCreatingBacktest}
                          className="bg-background border border-border text-foreground font-mono text-xs h-8"
                        />
                      </div>
                      <div className="space-y-1">
                        <label className="text-[10px] font-mono uppercase text-muted-foreground">Timespan</label>
                        <select
                          value={cooldownTimespan}
                          onChange={event =>
                            setCooldownTimespan(event.target.value as (typeof TIMESPAN_OPTIONS)[number])
                          }
                          disabled={isCreatingBacktest}
                          className="w-full bg-background border border-border text-foreground font-mono text-xs uppercase px-2 py-1 h-8 rounded-md"
                        >
                          {TIMESPAN_OPTIONS.map(option => (
                            <option key={`cooldown-${option}`} value={option}>
                              {option}
                            </option>
                          ))}
                        </select>
                      </div>
                    </div>
                  )}
                </div>

                <div className="border-t border-border pt-3">
                  <div className="flex items-center justify-between">
                    <div>
                      <div className="text-[10px] font-mono uppercase text-muted-foreground">Allow Simultaneous</div>
                      <div className="text-[10px] font-mono text-muted-foreground/70">Permit overlapping positions</div>
                    </div>
                    <Switch
                      checked={allowSimultaneous}
                      onCheckedChange={setAllowSimultaneous}
                      disabled={isCreatingBacktest}
                    />
                  </div>
                </div>
              </TabsContent>

              <TabsContent value="exit" className="space-y-4 mt-0">
                {/* Take Profit */}
                <div className="space-y-3 border border-border rounded-md p-3 bg-muted/10">
                  <div className="flex items-center justify-between">
                    <div>
                      <div className="text-[10px] font-mono uppercase text-green-600 dark:text-green-400">Take Profit</div>
                      <div className="text-[10px] font-mono text-muted-foreground/70">Exit on profit target</div>
                    </div>
                    <Switch
                      checked={takeProfitEnabled}
                      onCheckedChange={setTakeProfitEnabled}
                      disabled={isCreatingBacktest}
                    />
                  </div>

                  {takeProfitEnabled && (
                    <div className="grid grid-cols-2 gap-2">
                      <div className="space-y-1">
                        <label className="text-[10px] font-mono uppercase text-muted-foreground">Value</label>
                        <Input
                          type="text"
                          value={takeProfitValueInput}
                          onChange={event => {
                            const val = event.target.value;
                            if (val === '' || val === '-' || val === '.' || /^-?\d*\.?\d*$/.test(val)) {
                              setTakeProfitValueInput(val);
                              const numVal = Number(val);
                              if (val !== '' && val !== '-' && val !== '.' && !isNaN(numVal)) {
                                setTakeProfitValue(numVal);
                              }
                            }
                          }}
                          onBlur={() => {
                            const numVal = Number(takeProfitValueInput);
                            if (takeProfitValueInput === '' || takeProfitValueInput === '-' || takeProfitValueInput === '.' || isNaN(numVal)) {
                              setTakeProfitValueInput(takeProfitValue.toString());
                            } else {
                              setTakeProfitValueInput(numVal.toString());
                              setTakeProfitValue(numVal);
                            }
                          }}
                          disabled={isCreatingBacktest}
                          className="bg-background border border-border text-foreground font-mono text-xs h-7"
                        />
                      </div>
                      <div className="space-y-1">
                        <label className="text-[10px] font-mono uppercase text-muted-foreground">Type</label>
                        <select
                          value={takeProfitType}
                          onChange={event => setTakeProfitType(event.target.value as 'percent' | 'value')}
                          disabled={isCreatingBacktest}
                          className="w-full bg-background border border-border text-foreground font-mono text-xs uppercase px-2 py-1 h-7 rounded-md"
                        >
                          <option value="percent">Percent</option>
                          <option value="value">Value</option>
                        </select>
                      </div>
                      <div className="space-y-1">
                        <label className="text-[10px] font-mono uppercase text-muted-foreground">Candle</label>
                        <select
                          value={takeProfitCandleType}
                          onChange={event =>
                            setTakeProfitCandleType(event.target.value as (typeof CANDLE_TYPE_OPTIONS)[number])
                          }
                          disabled={isCreatingBacktest}
                          className="w-full bg-background border border-border text-foreground font-mono text-xs uppercase px-2 py-1 h-7 rounded-md"
                        >
                          {CANDLE_TYPE_OPTIONS.map(option => (
                            <option key={`tp-candle-${option}`} value={option}>
                              {option}
                            </option>
                          ))}
                        </select>
                      </div>
                      <div className="space-y-1">
                        <label className="text-[10px] font-mono uppercase text-muted-foreground">Price</label>
                        <select
                          value={takeProfitPriceActionType}
                          onChange={event =>
                            setTakeProfitPriceActionType(
                              event.target.value as (typeof PRICE_ACTION_OPTIONS)[number]
                            )
                          }
                          disabled={isCreatingBacktest}
                          className="w-full bg-background border border-border text-foreground font-mono text-xs uppercase px-2 py-1 h-7 rounded-md"
                        >
                          {PRICE_ACTION_OPTIONS.map(option => (
                            <option key={`tp-price-${option}`} value={option}>
                              {option}
                            </option>
                          ))}
                        </select>
                      </div>
                    </div>
                  )}
                </div>

                {/* Stop Loss */}
                <div className="space-y-3 border border-border rounded-md p-3 bg-muted/10">
                  <div className="flex items-center justify-between">
                    <div>
                      <div className="text-[10px] font-mono uppercase text-red-600 dark:text-red-400">Stop Loss</div>
                      <div className="text-[10px] font-mono text-muted-foreground/70">Protective stop</div>
                    </div>
                    <Switch
                      checked={stopLossEnabled}
                      onCheckedChange={setStopLossEnabled}
                      disabled={isCreatingBacktest}
                    />
                  </div>

                  {stopLossEnabled && (
                    <div className="grid grid-cols-2 gap-2">
                      <div className="space-y-1">
                        <label className="text-[10px] font-mono uppercase text-muted-foreground">Value</label>
                        <Input
                          type="text"
                          value={stopLossValueInput}
                          onChange={event => {
                            const val = event.target.value;
                            if (val === '' || val === '-' || val === '.' || /^-?\d*\.?\d*$/.test(val)) {
                              setStopLossValueInput(val);
                              const numVal = Number(val);
                              if (val !== '' && val !== '-' && val !== '.' && !isNaN(numVal)) {
                                setStopLossValue(numVal);
                              }
                            }
                          }}
                          onBlur={() => {
                            const numVal = Number(stopLossValueInput);
                            if (stopLossValueInput === '' || stopLossValueInput === '-' || stopLossValueInput === '.' || isNaN(numVal)) {
                              setStopLossValueInput(stopLossValue.toString());
                            } else {
                              setStopLossValueInput(numVal.toString());
                              setStopLossValue(numVal);
                            }
                          }}
                          disabled={isCreatingBacktest}
                          className="bg-background border border-border text-foreground font-mono text-xs h-7"
                        />
                      </div>
                      <div className="space-y-1">
                        <label className="text-[10px] font-mono uppercase text-muted-foreground">Type</label>
                        <select
                          value={stopLossType}
                          onChange={event => setStopLossType(event.target.value as 'percent' | 'value')}
                          disabled={isCreatingBacktest}
                          className="w-full bg-background border border-border text-foreground font-mono text-xs uppercase px-2 py-1 h-7 rounded-md"
                        >
                          <option value="percent">Percent</option>
                          <option value="value">Value</option>
                        </select>
                      </div>
                      <div className="space-y-1">
                        <label className="text-[10px] font-mono uppercase text-muted-foreground">Candle</label>
                        <select
                          value={stopLossCandleType}
                          onChange={event =>
                            setStopLossCandleType(event.target.value as (typeof CANDLE_TYPE_OPTIONS)[number])
                          }
                          disabled={isCreatingBacktest}
                          className="w-full bg-background border border-border text-foreground font-mono text-xs uppercase px-2 py-1 h-7 rounded-md"
                        >
                          {CANDLE_TYPE_OPTIONS.map(option => (
                            <option key={`sl-candle-${option}`} value={option}>
                              {option}
                            </option>
                          ))}
                        </select>
                      </div>
                      <div className="space-y-1">
                        <label className="text-[10px] font-mono uppercase text-muted-foreground">Price</label>
                        <select
                          value={stopLossPriceActionType}
                          onChange={event =>
                            setStopLossPriceActionType(
                              event.target.value as (typeof PRICE_ACTION_OPTIONS)[number]
                            )
                          }
                          disabled={isCreatingBacktest}
                          className="w-full bg-background border border-border text-foreground font-mono text-xs uppercase px-2 py-1 h-7 rounded-md"
                        >
                          {PRICE_ACTION_OPTIONS.map(option => (
                            <option key={`sl-price-${option}`} value={option}>
                              {option}
                            </option>
                          ))}
                        </select>
                      </div>
                    </div>
                  )}
                </div>

                {/* Timed Exit */}
                <div className="space-y-3 border border-border rounded-md p-3 bg-muted/10">
                  <div className="flex items-center justify-between">
                    <div>
                      <div className="text-[10px] font-mono uppercase text-amber-600 dark:text-amber-400">Timed Exit</div>
                      <div className="text-[10px] font-mono text-muted-foreground/70">Force exit after duration</div>
                    </div>
                    <Switch
                      checked={timedExitEnabled}
                      onCheckedChange={setTimedExitEnabled}
                      disabled={isCreatingBacktest}
                    />
                  </div>

                  {timedExitEnabled && (
                    <div className="grid grid-cols-2 gap-2">
                      <div className="space-y-1">
                        <label className="text-[10px] font-mono uppercase text-muted-foreground">Multiplier</label>
                        <Input
                          type="text"
                          value={timedExitMultiplier}
                          onChange={event => {
                            const val = event.target.value;
                            if (val === '' || (!isNaN(Number(val)) && Number(val) >= 1)) {
                              setTimedExitMultiplier(val === '' ? 1 : Number(val));
                            }
                          }}
                          disabled={isCreatingBacktest}
                          className="bg-background border border-border text-foreground font-mono text-xs h-7"
                        />
                      </div>
                      <div className="space-y-1">
                        <label className="text-[10px] font-mono uppercase text-muted-foreground">Timespan</label>
                        <select
                          value={timedExitTimespan}
                          onChange={event =>
                            setTimedExitTimespan(event.target.value as (typeof TIMESPAN_OPTIONS)[number])
                          }
                          disabled={isCreatingBacktest}
                          className="w-full bg-background border border-border text-foreground font-mono text-xs uppercase px-2 py-1 h-7 rounded-md"
                        >
                          {TIMESPAN_OPTIONS.map(option => (
                            <option key={`timed-${option}`} value={option}>
                              {option}
                            </option>
                          ))}
                        </select>
                      </div>
                    </div>
                  )}
                </div>
              </TabsContent>
            </Tabs>
          </Card>
        </div>
      </div>
    </div>
  );
}
