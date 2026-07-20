import { useMemo } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { BacktestReport, BenchmarkBar, RailRow } from '../components/backtest/BacktestReport';
import { backtestApi } from '../api/backtestApi';
import { normalizeTradingData } from '../utils/backtestNormalize';
import { SHARE_SCHEMA_VERSION, BacktestSharePayload, ShareConfig } from '../types/share';
import { Button } from '../components/ui/button';
import { Card } from '../components/ui/card';
import { formatDateNoTimezone } from '../utils/dateFormatter';
import { Lock, Share2 } from 'lucide-react';

function formatStopConfig(config: { priceActionType?: string; type?: string; value?: number } | undefined) {
  if (!config) return 'Not set';

  const priceActionDisplay = config.priceActionType ? `${config.priceActionType} ` : '';

  if (config.type === 'percent') {
    return `${priceActionDisplay}${config.value}%`;
  }
  if (config.type === 'flat' || config.type === 'value') {
    return `${priceActionDisplay}$${config.value}`;
  }
  return `${priceActionDisplay}${config.value} (${config.type || 'unknown type'})`;
}

function formatTimeframe(timeframe: { multiplier?: number; timespan?: string } | undefined) {
  if (!timeframe) return 'Not set';
  return `${timeframe.multiplier} ${timeframe.timespan}${(timeframe.multiplier ?? 1) > 1 ? 's' : ''}`;
}

/** Locked teaser rail rendered from counts only — the payload carries no config values. */
function MaskedConfigRail({ config }: { config: ShareConfig }) {
  const exitParts = [
    config.hasStopLoss ? 'stop loss' : null,
    config.hasProfitTarget ? 'take profit' : null,
    config.hasTimedExit ? 'timed exit' : null,
  ].filter(Boolean);

  return (
    <Card className="p-4">
      <div className="mb-2 flex items-center gap-2 text-[11px] uppercase tracking-widest text-muted-foreground">
        <Lock className="h-3.5 w-3.5" />
        Strategy configuration
      </div>
      <p className="mb-3 text-sm">
        Hidden by owner —{' '}
        <b className="font-semibold">
          {config.entryFilterCount ?? 0} entry filter{(config.entryFilterCount ?? 0) === 1 ? '' : 's'}
        </b>
        {exitParts.length > 0 && <> · {exitParts.join(' · ')}</>}
      </p>
      <div aria-hidden className="flex select-none flex-col gap-1.5 blur-[5px]">
        {Array.from({ length: Math.min(Math.max(config.entryFilterCount ?? 0, 2), 5) }).map((_, i) => (
          <code
            key={i}
            className="rounded-md border border-border/60 bg-muted/50 px-2.5 py-1.5 font-mono text-xs"
          >
            hidden filter expression {i + 1}
          </code>
        ))}
      </div>
      <p className="mt-3 text-xs text-muted-foreground">
        Build strategies like this with your own filters —{' '}
        <Link to="/sign-up" className="font-semibold underline">
          create a free account
        </Link>
        .
      </p>
    </Card>
  );
}

function ConfigRail({ config }: { config: ShareConfig }) {
  if (config.masked) {
    return (
      <aside className="flex flex-col gap-4 self-start lg:sticky lg:top-4">
        <MaskedConfigRail config={config} />
      </aside>
    );
  }

  const filters = config.entrySettings?.filters ?? [];
  const position = config.positionSettings;
  const exits = config.exitSettings;

  return (
    <aside className="flex flex-col gap-4 self-start lg:sticky lg:top-4">
      {filters.length > 0 && (
        <Card className="p-4">
          <h3 className="mb-2 text-[11px] uppercase tracking-widest text-muted-foreground">
            Entry filters
          </h3>
          <div className="flex flex-col gap-1.5">
            {filters.map((filter, index) => (
              <code
                key={index}
                className="rounded-md border border-border/60 bg-muted/50 px-2.5 py-1.5 font-mono text-xs"
              >
                {filter}
              </code>
            ))}
          </div>
        </Card>
      )}

      {position && (
        <Card className="p-4">
          <h3 className="mb-1 text-[11px] uppercase tracking-widest text-muted-foreground">
            Position
          </h3>
          <RailRow
            label="Starting balance"
            value={`$${(position.startingBalance ?? 0).toLocaleString()}`}
          />
          <RailRow
            label="Position size"
            value={`$${(position.model?.size ?? 0).toLocaleString()} ${(position.model?.type ?? 'Fixed').toLowerCase()}`}
          />
          <RailRow
            label="Max concurrent"
            value={String(position.maxConcurrentPositions ?? '—')}
          />
          {position.allowSimultaneous !== undefined && (
            <RailRow
              label="Simultaneous entries"
              value={position.allowSimultaneous ? 'Allowed' : 'Not allowed'}
            />
          )}
        </Card>
      )}

      {exits && (exits.stopLoss || exits.takeProfit || exits.timedExit) && (
        <Card className="p-4">
          <h3 className="mb-1 text-[11px] uppercase tracking-widest text-muted-foreground">
            Exits
          </h3>
          {exits.stopLoss && (
            <RailRow
              label="Stop loss"
              value={formatStopConfig(exits.stopLoss)}
              valueColor="var(--chart-loss)"
            />
          )}
          {exits.takeProfit && (
            <RailRow
              label="Take profit"
              value={formatStopConfig(exits.takeProfit)}
              valueColor="var(--chart-gain)"
            />
          )}
          {exits.timedExit?.timeframe && (
            <RailRow label="Timed exit" value={formatTimeframe(exits.timedExit.timeframe)} />
          )}
          {exits.timedExit?.avoidOvernight !== undefined && (
            <RailRow
              label="Overnight"
              value={exits.timedExit.avoidOvernight ? 'Avoided' : 'Allowed'}
            />
          )}
        </Card>
      )}
    </aside>
  );
}

function ShellCard({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen bg-background p-4 pt-20 text-foreground md:p-8 md:pt-8">
      <div className="mx-auto max-w-[1240px]">
        <Card className="space-y-3 p-12 text-center">{children}</Card>
      </div>
    </div>
  );
}

export function SharedBacktestPage() {
  const { shareId } = useParams<{ shareId: string }>();

  const { data: payload, isLoading, isError } = useQuery<BacktestSharePayload | null>({
    queryKey: ['sharedBacktest', shareId],
    queryFn: () => backtestApi.getShare(shareId!),
    enabled: !!shareId,
    staleTime: Infinity,
    retry: 1,
  });

  const tradingData = useMemo(
    () => (payload ? normalizeTradingData(payload.result) : null),
    [payload]
  );

  const benchmarkBars = useMemo<BenchmarkBar[] | null>(() => {
    if (!payload?.benchmark?.length) return null;
    return payload.benchmark.map((pt) => ({
      day: pt.date.slice(0, 10),
      close: pt.close,
    }));
  }, [payload]);

  if (isLoading) {
    return (
      <ShellCard>
        <div className="text-xs uppercase tracking-widest text-muted-foreground">Loading</div>
        <div className="text-base">Fetching shared backtest…</div>
      </ShellCard>
    );
  }

  const schemaUnsupported = payload != null && payload.schemaVersion !== SHARE_SCHEMA_VERSION;

  if (isError || !payload || schemaUnsupported || !tradingData) {
    return (
      <ShellCard>
        <Share2 className="mx-auto h-8 w-8 text-muted-foreground" />
        <h1 className="text-lg font-semibold">
          This shared backtest has expired or doesn't exist
        </h1>
        <p className="mx-auto max-w-md text-sm text-muted-foreground">
          Share links expire 30 days after they're created. Ask the owner for a fresh link —
          or run your own backtests with a free StockMountain account.
        </p>
        <div>
          <Link to="/sign-up">
            <Button>Try StockMountain</Button>
          </Link>
        </div>
      </ShellCard>
    );
  }

  const startingBalance =
    payload.config.positionSettings?.startingBalance ||
    tradingData.hold.equity[0]?.startCash ||
    10000;
  const tradingDays = tradingData.hold.equity.length;
  const totalTrades =
    tradingData.hold.stats.totalTradesTaken ?? tradingData.hold.trades.length ?? 0;

  return (
    <div className="min-h-screen bg-background p-4 pt-20 text-foreground md:p-8 md:pt-8">
      <div className="mx-auto max-w-[1240px]">
        {/* ---------- Masthead ---------- */}
        <header className="mb-6 flex flex-wrap items-end gap-4 border-b-2 border-foreground/80 pb-5">
          <div>
            <div className="mb-1.5 flex items-center gap-3">
              <span className="inline-flex items-center gap-1.5 rounded-full bg-muted px-2.5 py-0.5 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
                <Share2 className="h-3 w-3" />
                Shared backtest · read-only
              </span>
              <span className="text-[11px] uppercase tracking-widest text-muted-foreground tabular-nums">
                expires {new Date(payload.expiresAt).toLocaleDateString()}
              </span>
            </div>
            <h1 className="text-2xl font-semibold tracking-tight md:text-3xl">
              {payload.title || 'Backtest report'}
            </h1>
            <p className="mt-1 text-[13px] text-muted-foreground tabular-nums">
              {formatDateNoTimezone(payload.start)} – {formatDateNoTimezone(payload.end)}
              {tradingDays > 0 && (
                <>
                  {' '}· <b className="font-semibold text-foreground">{tradingDays} trading days</b>
                </>
              )}
              {totalTrades > 0 && <> · {totalTrades.toLocaleString()} trades</>}
            </p>
          </div>
          <div className="ml-auto flex gap-2 pb-1">
            <Link to="/sign-up">
              <Button size="sm">Try StockMountain</Button>
            </Link>
          </div>
        </header>

        <BacktestReport
          tradingData={tradingData}
          startingBalance={startingBalance}
          benchmarkBars={benchmarkBars}
          configRail={<ConfigRail config={payload.config} />}
        />

        <div className="mb-8 mt-2 text-center text-xs text-muted-foreground">
          Snapshot created {new Date(payload.createdAt).toLocaleDateString()} · data is frozen at
          share time ·{' '}
          <Link to="/sign-up" className="font-semibold underline">
            run your own backtests free
          </Link>
        </div>
      </div>
    </div>
  );
}

export default SharedBacktestPage;
