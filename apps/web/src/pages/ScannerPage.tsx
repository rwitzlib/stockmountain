import { useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useUser } from '@clerk/react';
import { Copy, Loader2, Plus, Search, Trash2 } from 'lucide-react';
import { scannerApi } from '../api/scannerApi';
import type { Scanner } from '../types/scanner';
import { FilterChips } from '../components/filters/FilterChips';
import { ScannerProGate } from '../components/scanner/ScannerProGate';
import { Card } from '../components/ui/card';
import { Button } from '../components/ui/button';
import { toast } from '../hooks/use-toast';

const VISIBLE_FILTER_COUNT = 3;

function ScannerCard({ scanner }: { scanner: Scanner }) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const deleteMutation = useMutation({
    mutationFn: scannerApi.deleteScanner,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['myScanners'] });
      toast({ title: 'Scanner deleted', description: `"${scanner.name}" has been deleted.` });
    },
    onError: () => {
      toast({
        title: 'Error',
        description: 'Failed to delete scanner. Please try again.',
        variant: 'destructive',
      });
    },
  });

  const filters = scanner.entrySettings.filters;
  const hiddenCount = filters.length - VISIBLE_FILTER_COUNT;

  return (
    <Card
      className="group flex cursor-pointer flex-col gap-3 p-5 transition-colors hover:border-muted-foreground/50"
      onClick={() => scanner.id && navigate(`/scanner/${scanner.id}`)}
    >
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <h3 className="truncate text-base font-semibold tracking-tight">{scanner.name}</h3>
          <p className="mt-0.5 text-xs text-muted-foreground tabular-nums">
            {filters.length} filter{filters.length !== 1 && 's'}
          </p>
        </div>
        <div
          className="flex items-center gap-1 opacity-0 transition-opacity group-hover:opacity-100"
          onClick={(event) => event.stopPropagation()}
        >
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="h-7 w-7 p-0 text-muted-foreground hover:text-foreground"
            title="Duplicate"
            onClick={() =>
              navigate('/scanner/new', {
                state: {
                  initialData: {
                    name: `${scanner.name} (copy)`,
                    entrySettings: scanner.entrySettings,
                  },
                },
              })
            }
          >
            <Copy className="h-4 w-4" />
          </Button>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="h-7 w-7 p-0 text-muted-foreground hover:text-red-500"
            title="Delete"
            disabled={deleteMutation.isPending || !scanner.id}
            onClick={() => {
              if (scanner.id && window.confirm(`Delete scanner "${scanner.name}"?`)) {
                deleteMutation.mutate(scanner.id);
              }
            }}
          >
            {deleteMutation.isPending ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <Trash2 className="h-4 w-4" />
            )}
          </Button>
        </div>
      </div>

      <div className="flex flex-col gap-1.5">
        {filters.slice(0, VISIBLE_FILTER_COUNT).map((filter, index) => (
          <div
            key={`${filter}-${index}`}
            className="rounded-md border border-border/60 bg-muted/50 px-2.5 py-1.5"
          >
            <FilterChips expression={filter} />
          </div>
        ))}
        {hiddenCount > 0 && (
          <p className="text-[11px] text-muted-foreground">+{hiddenCount} more</p>
        )}
      </div>
    </Card>
  );
}

export function ScannerPage() {
  const navigate = useNavigate();
  const { user } = useUser();

  // Keyed by user so a sign-out/sign-in switch can never serve another user's cache.
  const { data: scanners = [], isLoading } = useQuery({
    queryKey: ['myScanners', user?.id],
    queryFn: scannerApi.getMyScanners,
    enabled: !!user?.id,
  });

  return (
    <ScannerProGate>
      <div className="min-h-screen bg-background p-4 pt-20 text-foreground md:p-8 md:pt-8">
        <div className="mx-auto max-w-[1240px]">
          {/* ---------- Masthead ---------- */}
          <header className="mb-6 flex flex-wrap items-end gap-4 border-b-2 border-foreground/80 pb-5">
            <div className="min-w-0 flex-1">
              <h1 className="text-2xl font-semibold tracking-tight md:text-3xl">Stock Scanner</h1>
              <p className="mt-1 text-[13px] text-muted-foreground tabular-nums">
                {scanners.length} saved scanner{scanners.length !== 1 && 's'}
              </p>
            </div>
            <div className="ml-auto pb-1">
              <Button size="sm" onClick={() => navigate('/scanner/new')}>
                <Plus className="mr-1.5 h-4 w-4" />
                New Scanner
              </Button>
            </div>
          </header>

          {isLoading ? (
            <Card className="p-8 text-center">
              <div className="mb-2 text-xs uppercase tracking-widest text-muted-foreground">Loading</div>
              <div className="text-base">Fetching scanners…</div>
            </Card>
          ) : scanners.length === 0 ? (
            <Card className="p-10 text-center">
              <Search className="mx-auto mb-3 h-8 w-8 text-muted-foreground" />
              <h2 className="text-lg font-semibold tracking-tight">No scanners yet</h2>
              <p className="mx-auto mt-2 max-w-md text-sm text-muted-foreground">
                A scanner is a saved set of entry filters you can run against live market
                data — the same filter language your strategies and backtests use. See the{' '}
                <a href="/docs/filters" className="underline hover:text-foreground">
                  filter reference
                </a>{' '}
                for what's possible.
              </p>
              <Button className="mt-5" onClick={() => navigate('/scanner/new')}>
                <Plus className="mr-1.5 h-4 w-4" />
                Create your first scanner
              </Button>
            </Card>
          ) : (
            <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
              {scanners.map((scanner) => (
                <ScannerCard key={scanner.id} scanner={scanner} />
              ))}
            </div>
          )}
        </div>
      </div>
    </ScannerProGate>
  );
}
