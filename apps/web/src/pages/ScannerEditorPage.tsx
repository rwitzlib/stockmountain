import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useUser } from '@clerk/react';
import {
  AlertCircle,
  ArrowDown,
  ArrowLeft,
  ArrowUp,
  FlaskConical,
  Loader2,
  Play,
  Save,
  TrendingUp,
} from 'lucide-react';
import type { Scanner, ScanResponse, ScanResultItem } from '../types/scanner';
import type { EntrySettings } from '../types/strategy';
import { scannerApi } from '../api/scannerApi';
import { Clock } from '../components/clock/Clock';
import { MarketStatus } from '../components/market';
import { ApiStatus } from '../components/status';
import { Card } from '../components/ui/card';
import { Button } from '../components/ui/button';
import { Switch } from '../components/ui/switch';
import { RailRow } from '../components/backtest/BacktestReport';
import { EntrySettingsForm } from '../components/forms/strategy/EntrySettingsForm';
import { SectionHeading } from '../components/forms/SectionHeading';
import { FilterChips } from '../components/filters/FilterChips';
import { ScannerProGate } from '../components/scanner/ScannerProGate';
import { toast } from '../hooks/use-toast';

const AUTO_REFRESH_MS = 30_000;
const RESULT_CAP = 1000;

/**
 * The scan engine has data 4:00–20:00 ET on weekdays (pre-market through after-hours).
 * Auto-refresh pauses outside that window — results wouldn't change.
 */
function isMarketSessionActive(): boolean {
  const eastern = new Date(new Date().toLocaleString('en-US', { timeZone: 'America/New_York' }));
  const day = eastern.getDay();
  if (day === 0 || day === 6) return false;
  const hours = eastern.getHours();
  return hours >= 4 && hours < 20;
}

type SortKey = keyof Pick<ScanResultItem, 'ticker' | 'price' | 'volume' | 'float'>;

interface SortState {
  key: SortKey;
  dir: 'asc' | 'desc';
}

const defaultFormData: Scanner = {
  name: '',
  entrySettings: { filters: [] },
};

const formatCompact = (value: number | undefined) =>
  value === undefined || value === null
    ? '—'
    : Intl.NumberFormat('en-US', { notation: 'compact', maximumFractionDigits: 1 }).format(value);

export function ScannerEditorPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const queryClient = useQueryClient();
  const { scannerId } = useParams<{ scannerId: string }>();
  const { user } = useUser();

  const isEditMode = !!scannerId;
  const [formData, setFormData] = useState<Scanner>(defaultFormData);
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);
  const initializedFromNavState = useRef(false);
  // Overlapping runs (auto-refresh + manual) resolve out of order — only the
  // latest request may apply its result.
  const scanSequence = useRef(0);

  const [scanResult, setScanResult] = useState<ScanResponse | null>(null);
  const [lastRunAt, setLastRunAt] = useState<Date | null>(null);
  const [isScanning, setIsScanning] = useState(false);
  const [autoRefresh, setAutoRefresh] = useState(false);
  const [completedBarsOnly, setCompletedBarsOnly] = useState(false);
  const [sort, setSort] = useState<SortState>({ key: 'volume', dir: 'desc' });

  // Keyed by user so a sign-out/sign-in switch can never serve another user's cache.
  const { data: existingScanner, isLoading: isLoadingScanner } = useQuery({
    queryKey: ['scanner', user?.id, scannerId],
    queryFn: () => scannerApi.getScanner(scannerId!),
    enabled: isEditMode && !!user?.id,
  });

  // Initialize from the fetched scanner, or from navigation state (strategy/backtest handoff)
  useEffect(() => {
    if (existingScanner) {
      setFormData({ ...defaultFormData, ...existingScanner });
      setHasUnsavedChanges(false);
    } else if (location.state?.initialData && !initializedFromNavState.current) {
      initializedFromNavState.current = true;
      const initial = location.state.initialData as Partial<Scanner>;
      setFormData({
        ...defaultFormData,
        ...initial,
        entrySettings: { filters: initial.entrySettings?.filters ?? [] },
      });
      setHasUnsavedChanges(true);
    }
  }, [existingScanner, location.state]);

  const filters = formData.entrySettings.filters;

  const update = (patch: Partial<Scanner>) => {
    setFormData((prev) => ({ ...prev, ...patch }));
    setHasUnsavedChanges(true);
  };

  const createMutation = useMutation({
    mutationFn: scannerApi.createScanner,
    onSuccess: (response) => {
      queryClient.invalidateQueries({ queryKey: ['myScanners'] });
      setHasUnsavedChanges(false);
      toast({ title: 'Scanner created', description: `"${formData.name}" has been saved.` });
      navigate(`/scanner/${response.id}`, { replace: true });
    },
    onError: () => {
      toast({
        title: 'Error',
        description: 'Failed to create scanner. Please try again.',
        variant: 'destructive',
      });
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: string; data: Scanner }) => scannerApi.updateScanner(id, data),
    // Awaiting the invalidations keeps isPending (and the input freeze) true until the
    // active scanner query has refetched — otherwise an edit made in that window would
    // be overwritten when the initialization effect copies the refetched snapshot in.
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['scanner'] }),
        queryClient.invalidateQueries({ queryKey: ['myScanners'] }),
      ]);
      setHasUnsavedChanges(false);
      toast({ title: 'Scanner updated', description: `"${formData.name}" has been saved.` });
    },
    onError: () => {
      toast({
        title: 'Error',
        description: 'Failed to update scanner. Please try again.',
        variant: 'destructive',
      });
    },
  });

  // Freeze editable controls while a save is in flight — edits made mid-save would be
  // discarded by the create navigation or the update refetch restoring the snapshot.
  const isSaving = createMutation.isPending || updateMutation.isPending;

  const handleSave = () => {
    if (!formData.name.trim()) {
      toast({
        title: 'Name required',
        description: 'Give the scanner a name before saving.',
        variant: 'destructive',
      });
      return;
    }
    if (filters.length === 0) {
      toast({
        title: 'No filters',
        description: 'Add at least one filter before saving.',
        variant: 'destructive',
      });
      return;
    }

    if (isEditMode && scannerId) {
      updateMutation.mutate({ id: scannerId, data: formData });
    } else {
      createMutation.mutate(formData);
    }
  };

  const runScan = useCallback(
    async (options?: { silent?: boolean }) => {
      if (filters.length === 0) {
        if (!options?.silent) {
          toast({
            title: 'No filters',
            description: 'Add at least one filter before running the scanner.',
            variant: 'destructive',
          });
        }
        return;
      }

      const sequence = ++scanSequence.current;
      setIsScanning(true);
      try {
        const result = await scannerApi.runScan(filters, completedBarsOnly);
        if (sequence !== scanSequence.current) return;
        setScanResult(result);
        setLastRunAt(new Date());
      } catch (e) {
        if (sequence !== scanSequence.current) return;
        if (!options?.silent) {
          toast({
            title: 'Scan failed',
            description: e instanceof Error ? e.message : 'Unable to run the scan.',
            variant: 'destructive',
          });
        }
      } finally {
        if (sequence === scanSequence.current) {
          setIsScanning(false);
        }
      }
    },
    [filters, completedBarsOnly],
  );

  // Auto-refresh: only while the page is open and the market session is active.
  useEffect(() => {
    if (!autoRefresh) return;

    const tick = () => {
      if (isMarketSessionActive()) {
        void runScan({ silent: true });
      }
    };
    const interval = setInterval(tick, AUTO_REFRESH_MS);
    return () => clearInterval(interval);
  }, [autoRefresh, runScan]);

  const handleSort = (key: SortKey) => {
    setSort((prev) =>
      prev.key === key ? { key, dir: prev.dir === 'asc' ? 'desc' : 'asc' } : { key, dir: 'desc' },
    );
  };

  const sortedItems = (scanResult?.items ?? []).slice().sort((a, b) => {
    const direction = sort.dir === 'asc' ? 1 : -1;
    if (sort.key === 'ticker') return a.ticker.localeCompare(b.ticker) * direction;
    return ((a[sort.key] ?? 0) - (b[sort.key] ?? 0)) * direction;
  });

  const sessionActive = isMarketSessionActive();

  const sortIndicator = (key: SortKey) =>
    sort.key === key ? (
      sort.dir === 'asc' ? (
        <ArrowUp className="ml-1 inline h-3 w-3" />
      ) : (
        <ArrowDown className="ml-1 inline h-3 w-3" />
      )
    ) : null;

  if (isEditMode && isLoadingScanner) {
    return (
      <div className="min-h-screen bg-background p-4 pt-20 text-foreground md:p-8 md:pt-8">
        <div className="mx-auto max-w-[1240px]">
          <Card className="p-8 text-center">
            <div className="mb-2 text-xs uppercase tracking-widest text-muted-foreground">Loading</div>
            <div className="text-base">Fetching scanner…</div>
          </Card>
        </div>
      </div>
    );
  }

  return (
    <ScannerProGate>
      <div className="min-h-screen bg-background p-4 pt-20 text-foreground md:p-8 md:pt-8">
        <div className="mx-auto max-w-[1240px]">
          {/* ---------- Masthead ---------- */}
          <header className="mb-6 flex flex-wrap items-end gap-4 border-b-2 border-foreground/80 pb-5">
            <div className="min-w-0 flex-1">
              <div className="mb-1.5 flex items-center gap-3">
                <Link
                  to="/scanner"
                  className="inline-flex items-center gap-1 text-xs uppercase tracking-widest text-muted-foreground transition-colors hover:text-foreground"
                >
                  <ArrowLeft className="h-3.5 w-3.5" />
                  Scanners
                </Link>
                <div className="flex items-center gap-2">
                  <Clock />
                  <MarketStatus />
                  <ApiStatus />
                </div>
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
                disabled={isSaving}
                placeholder="Untitled scanner"
                autoFocus={!isEditMode}
                className="w-full max-w-2xl border-b border-transparent bg-transparent text-2xl font-semibold tracking-tight outline-none transition-colors placeholder:text-muted-foreground/50 focus:border-border md:text-3xl"
              />
              <p className="mt-1 text-[13px] text-muted-foreground tabular-nums">
                {filters.length} filter{filters.length !== 1 && 's'}
                {scanResult && ` · ${scanResult.items.length} match${scanResult.items.length !== 1 ? 'es' : ''}`}
              </p>
            </div>
            <div className="ml-auto flex gap-2 pb-1">
              <Button
                size="sm"
                variant="outline"
                onClick={handleSave}
                disabled={isSaving}
              >
                {isSaving ? (
                  <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />
                ) : (
                  <Save className="mr-1.5 h-4 w-4" />
                )}
                {isEditMode ? 'Save changes' : 'Save scanner'}
              </Button>
              <Button size="sm" onClick={() => runScan()} disabled={isScanning}>
                {isScanning ? (
                  <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />
                ) : (
                  <Play className="mr-1.5 h-4 w-4" />
                )}
                Run Scan
              </Button>
            </div>
          </header>

          <div className="grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,1fr)_320px]">
            {/* ---------- Main column ---------- */}
            <div className="flex min-w-0 flex-col gap-6">
              <Card className="p-5">
                <SectionHeading
                  index="01"
                  label="Filters"
                  title="Scan conditions"
                  hint="Tickers matching every filter show up in the results — same filter language as strategies and backtests."
                />
                <fieldset disabled={isSaving} className={isSaving ? 'opacity-60' : undefined}>
                  <EntrySettingsForm
                    value={formData.entrySettings}
                    onChange={(entrySettings: EntrySettings) => update({ entrySettings })}
                    context="scan"
                  />
                </fieldset>
              </Card>

              <Card className="p-5">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <SectionHeading
                    index="02"
                    label="Results"
                    title="Matching tickers"
                    hint={
                      scanResult
                        ? `${scanResult.items.length} match${scanResult.items.length !== 1 ? 'es' : ''} · ${scanResult.timeElapsed} ms`
                        : 'Run the scan to see which tickers pass every filter right now.'
                    }
                  />
                  <div className="flex flex-col items-end gap-2 pt-1">
                    <label className="flex cursor-pointer items-center gap-2 text-xs text-muted-foreground">
                      Auto-refresh (30s)
                      <Switch checked={autoRefresh} onCheckedChange={setAutoRefresh} />
                    </label>
                    <label className="flex cursor-pointer items-center gap-2 text-xs text-muted-foreground">
                      Completed bars only
                      <Switch checked={completedBarsOnly} onCheckedChange={setCompletedBarsOnly} />
                    </label>
                  </div>
                </div>

                {autoRefresh && !sessionActive && (
                  <p className="mb-3 rounded-md border border-amber-300/50 bg-amber-100/30 px-3 py-2 text-xs text-amber-700 dark:border-amber-800 dark:bg-amber-950/30 dark:text-amber-400">
                    Market closed — auto-refresh resumes when the session opens (4:00–20:00 ET, weekdays).
                  </p>
                )}

                {scanResult === null ? (
                  <div className="rounded-lg border-2 border-dashed border-border p-8 text-center">
                    <TrendingUp className="mx-auto mb-2 h-6 w-6 text-muted-foreground" />
                    <p className="text-sm text-muted-foreground">No results yet — hit Run Scan.</p>
                  </div>
                ) : sortedItems.length === 0 ? (
                  <div className="rounded-lg border-2 border-dashed border-border p-8 text-center">
                    <p className="text-sm text-muted-foreground">No tickers match right now.</p>
                  </div>
                ) : (
                  <>
                    {scanResult.items.length >= RESULT_CAP && (
                      <p className="mb-2 text-xs text-muted-foreground">
                        Results are capped at {RESULT_CAP} matches — tighten the filters to narrow
                        it down.
                      </p>
                    )}
                    <div className="overflow-x-auto">
                      <table className="w-full text-sm">
                        <thead>
                          <tr className="border-b border-border text-left text-[11px] uppercase tracking-widest text-muted-foreground">
                            {(
                              [
                                { key: 'ticker', label: 'Ticker', align: 'left' },
                                { key: 'price', label: 'Price', align: 'right' },
                                { key: 'volume', label: 'Volume', align: 'right' },
                                { key: 'float', label: 'Float', align: 'right' },
                              ] as { key: SortKey; label: string; align: 'left' | 'right' }[]
                            ).map(({ key, label, align }) => (
                              <th
                                key={key}
                                className={`py-2 ${key !== 'float' ? 'pr-4' : ''} ${align === 'right' ? 'text-right' : ''}`}
                                aria-sort={
                                  sort.key === key
                                    ? sort.dir === 'asc'
                                      ? 'ascending'
                                      : 'descending'
                                    : undefined
                                }
                              >
                                <button
                                  type="button"
                                  onClick={() => handleSort(key)}
                                  className="select-none uppercase tracking-widest hover:text-foreground"
                                >
                                  {label}
                                  {sortIndicator(key)}
                                </button>
                              </th>
                            ))}
                          </tr>
                        </thead>
                        <tbody>
                          {sortedItems.map((item) => (
                            <tr
                              key={item.ticker}
                              className="border-b border-border/40 transition-colors hover:bg-accent/40"
                            >
                              <td className="py-1.5 pr-4 font-mono font-medium">{item.ticker}</td>
                              <td className="py-1.5 pr-4 text-right tabular-nums">
                                ${item.price.toFixed(2)}
                              </td>
                              <td className="py-1.5 pr-4 text-right tabular-nums">
                                {formatCompact(item.volume)}
                              </td>
                              <td className="py-1.5 text-right tabular-nums">
                                {formatCompact(item.float)}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </>
                )}
              </Card>
            </div>

            {/* ---------- Config rail ---------- */}
            <aside className="flex flex-col gap-4 self-start lg:sticky lg:top-4">
              <Card className="p-4">
                <h3 className="mb-2 text-[11px] uppercase tracking-widest text-muted-foreground">
                  Filters
                </h3>
                {filters.length > 0 ? (
                  <div className="flex flex-col gap-1.5">
                    {filters.map((filter, index) => (
                      <div
                        key={`${filter}-${index}`}
                        className="rounded-md border border-border/60 bg-muted/50 px-2.5 py-1.5"
                      >
                        <FilterChips expression={filter} />
                      </div>
                    ))}
                  </div>
                ) : (
                  <p className="text-[13px] text-muted-foreground">
                    None yet — a scanner needs at least one.
                  </p>
                )}
              </Card>

              <Card className="p-4">
                <h3 className="mb-1 text-[11px] uppercase tracking-widest text-muted-foreground">
                  Last run
                </h3>
                <RailRow
                  label="Matches"
                  value={scanResult ? String(scanResult.items.length) : '—'}
                />
                <RailRow
                  label="Elapsed"
                  value={scanResult ? `${scanResult.timeElapsed} ms` : '—'}
                />
                <RailRow
                  label="At"
                  value={lastRunAt ? lastRunAt.toLocaleTimeString() : '—'}
                />
              </Card>

              <Card className="p-4">
                <h3 className="mb-2 text-[11px] uppercase tracking-widest text-muted-foreground">
                  Take it further
                </h3>
                <div className="flex flex-col gap-2">
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    disabled={filters.length === 0}
                    onClick={() =>
                      navigate('/backtest/create', {
                        state: {
                          backtestDefaults: { entrySettings: { filters } },
                        },
                      })
                    }
                  >
                    <FlaskConical className="mr-1.5 h-3.5 w-3.5" />
                    Backtest these filters
                  </Button>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    disabled={filters.length === 0}
                    onClick={() =>
                      navigate('/strategies/new', {
                        state: {
                          initialData: {
                            name: formData.name,
                            entrySettings: { filters },
                          },
                        },
                      })
                    }
                  >
                    <TrendingUp className="mr-1.5 h-3.5 w-3.5" />
                    Promote to strategy
                  </Button>
                </div>
              </Card>
            </aside>
          </div>
        </div>
      </div>
    </ScannerProGate>
  );
}
