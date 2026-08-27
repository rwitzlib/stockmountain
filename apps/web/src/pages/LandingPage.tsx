import { useEffect, useRef, useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useUser } from '@clerk/react';
import {
  ArrowRight,
  BarChart3,
  Bot,
  CandlestickChart,
  Check,
  EyeOff,
  FlaskConical,
  Lock,
  Search,
  Share2,
  SlidersHorizontal,
} from 'lucide-react';
import { cn } from '../utils/utils';

/* ------------------------------------------------------------------ */
/* Shared bits                                                         */
/* ------------------------------------------------------------------ */

function Wordmark({ className }: { className?: string }) {
  return (
    <span className={cn('inline-flex items-center gap-2', className)}>
      <svg
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
        className="h-5 w-5"
        aria-hidden
      >
        <path d="M2 20 L8.5 8 L12.5 14 L17.5 4 L22 20" />
        <path d="M2 20 H22" opacity="0.35" />
      </svg>
      <span className="text-sm font-semibold tracking-tight">StockMountain</span>
    </span>
  );
}

/** Fades content up when scrolled into view. */
function Reveal({
  children,
  className,
  delay = 0,
}: {
  children: ReactNode;
  className?: string;
  delay?: number;
}) {
  const ref = useRef<HTMLDivElement>(null);
  const [shown, setShown] = useState(false);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    if (typeof IntersectionObserver === 'undefined') {
      setShown(true);
      return;
    }
    const obs = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          setShown(true);
          obs.disconnect();
        }
      },
      { threshold: 0.15 },
    );
    obs.observe(el);
    return () => obs.disconnect();
  }, []);

  return (
    <div
      ref={ref}
      style={{ transitionDelay: `${delay}ms` }}
      className={cn(
        'transition-all duration-700 ease-out',
        shown ? 'translate-y-0 opacity-100' : 'translate-y-6 opacity-0',
        className,
      )}
    >
      {children}
    </div>
  );
}

/* Faded candlestick runs that decorate section margins — an ascending rally with pullbacks. */
const CANDLES: { x: number; bodyY: number; bodyH: number; wickY: number; wickH: number; kind: 'gain' | 'loss' }[] = [
  { x: 6, bodyY: 452, bodyH: 44, wickY: 440, wickH: 68, kind: 'gain' },
  { x: 40, bodyY: 414, bodyH: 40, wickY: 402, wickH: 64, kind: 'gain' },
  { x: 74, bodyY: 424, bodyH: 30, wickY: 414, wickH: 56, kind: 'loss' },
  { x: 108, bodyY: 364, bodyH: 52, wickY: 350, wickH: 78, kind: 'gain' },
  { x: 142, bodyY: 322, bodyH: 42, wickY: 310, wickH: 68, kind: 'gain' },
  { x: 176, bodyY: 336, bodyH: 28, wickY: 326, wickH: 54, kind: 'loss' },
  { x: 210, bodyY: 268, bodyH: 50, wickY: 254, wickH: 76, kind: 'gain' },
  { x: 244, bodyY: 222, bodyH: 44, wickY: 210, wickH: 70, kind: 'gain' },
  { x: 278, bodyY: 170, bodyH: 48, wickY: 156, wickH: 76, kind: 'gain' },
];

function CandleSticks({
  direction = 'up',
  className,
}: {
  direction?: 'up' | 'down';
  className?: string;
}) {
  const candles =
    direction === 'up'
      ? CANDLES
      : CANDLES.map(({ x, bodyY, bodyH, wickY, wickH, kind }) => ({
          x,
          bodyY: 560 - bodyY - bodyH,
          bodyH,
          wickY: 560 - wickY - wickH,
          wickH,
          kind: (kind === 'gain' ? 'loss' : 'gain') as 'gain' | 'loss',
        }));
  return (
    <div
      aria-hidden
      className={cn('pointer-events-none absolute hidden lg:block', className)}
      style={{
        maskImage: 'linear-gradient(to bottom, transparent, black 25%, black 75%, transparent)',
        WebkitMaskImage:
          'linear-gradient(to bottom, transparent, black 25%, black 75%, transparent)',
      }}
    >
      <svg viewBox="0 0 320 560" fill="none" className="h-full w-full" preserveAspectRatio="none">
        {candles.map(({ x, bodyY, bodyH, wickY, wickH, kind }) => {
          const color = kind === 'gain' ? 'var(--chart-gain)' : 'var(--chart-loss)';
          const cx = x + 8;
          return (
            <g key={x}>
              <line
                x1={cx}
                y1={wickY}
                x2={cx}
                y2={wickY + wickH}
                stroke={color}
                strokeOpacity="0.25"
                strokeWidth="1.5"
              />
              <rect
                x={x}
                y={bodyY}
                width="16"
                height={bodyH}
                rx="2"
                fill={color}
                fillOpacity="0.14"
                stroke={color}
                strokeOpacity="0.35"
                strokeWidth="1.5"
              />
            </g>
          );
        })}
      </svg>
    </div>
  );
}

function Eyebrow({ children }: { children: ReactNode }) {
  return (
    <div className="mb-3 font-mono text-[11px] font-medium uppercase tracking-widest text-muted-foreground">
      {children}
    </div>
  );
}

function FilterChip({ children, dim }: { children: ReactNode; dim?: boolean }) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-md border border-border bg-muted/60 px-2.5 py-1 font-mono text-xs text-foreground',
        dim && 'select-none text-muted-foreground blur-[3px]',
      )}
    >
      {children}
    </span>
  );
}

/* ------------------------------------------------------------------ */
/* Hero report mock                                                    */
/* ------------------------------------------------------------------ */

const STRATEGY_POINTS =
  '0,252 30,246 55,250 80,238 105,242 130,226 150,232 175,214 200,220 220,200 245,206 265,188 285,196 305,172 325,180 345,158 365,166 385,142 405,152 425,126 445,136 465,146 480,152 500,128 520,112 540,120 560,96 580,104 600,84 620,92 640,70 660,80 680,58 700,66 720,46 740,54 760,36 780,42 800,26';

const BENCHMARK_POINTS =
  '0,252 80,246 160,238 240,242 320,228 400,232 480,218 560,222 640,206 720,210 800,192';

const CEILING_POINTS =
  '0,248 80,226 160,204 240,188 320,158 400,132 480,128 560,84 640,58 720,34 800,12';

function EquityCurve() {
  const strategyPath = `M${STRATEGY_POINTS.split(' ').join(' L')}`;
  const areaPath = `${strategyPath} L800,280 L0,280 Z`;

  return (
    <svg viewBox="0 0 800 280" preserveAspectRatio="none" className="h-full w-full" aria-hidden>
      <defs>
        <linearGradient id="landing-equity-fill" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="var(--chart-strategy)" stopOpacity="0.22" />
          <stop offset="100%" stopColor="var(--chart-strategy)" stopOpacity="0" />
        </linearGradient>
      </defs>

      {[56, 112, 168, 224].map((y) => (
        <line key={y} x1="0" y1={y} x2="800" y2={y} stroke="hsl(var(--border))" strokeWidth="1" />
      ))}

      <path d={areaPath} fill="url(#landing-equity-fill)" className="landing-fade-in" />

      <polyline
        points={CEILING_POINTS}
        fill="none"
        stroke="var(--chart-ceiling)"
        strokeWidth="1.5"
        strokeOpacity="0.45"
        pathLength={1}
        className="landing-draw-slow"
      />
      <polyline
        points={BENCHMARK_POINTS}
        fill="none"
        stroke="var(--chart-benchmark)"
        strokeWidth="1.5"
        strokeDasharray="5 4"
        strokeOpacity="0.8"
      />
      <polyline
        points={STRATEGY_POINTS}
        fill="none"
        stroke="var(--chart-strategy)"
        strokeWidth="2.25"
        strokeLinejoin="round"
        pathLength={1}
        className="landing-draw"
      />

      <circle cx="800" cy="26" r="4" fill="var(--chart-strategy)" className="landing-pulse" />
    </svg>
  );
}

function LegendDot({ color, dashed, label }: { color: string; dashed?: boolean; label: string }) {
  return (
    <span className="inline-flex items-center gap-1.5 text-xs text-muted-foreground">
      <svg width="18" height="6" aria-hidden>
        <line
          x1="0"
          y1="3"
          x2="18"
          y2="3"
          stroke={color}
          strokeWidth="2"
          strokeDasharray={dashed ? '4 3' : undefined}
        />
      </svg>
      {label}
    </span>
  );
}

function KpiTile({ label, value, sub }: { label: string; value: string; sub?: string }) {
  return (
    <div className="rounded-lg border border-border/80 bg-background/60 p-3">
      <div className="text-[10px] font-medium uppercase tracking-wider text-muted-foreground">
        {label}
      </div>
      <div className="mt-0.5 font-mono text-lg font-semibold tabular-nums text-foreground">
        {value}
      </div>
      {sub && <div className="text-[11px] text-muted-foreground">{sub}</div>}
    </div>
  );
}

function ReportMock() {
  return (
    <div className="overflow-hidden rounded-xl border border-border/80 bg-card shadow-2xl shadow-black/20">
      <div className="flex items-center justify-between border-b border-border/80 px-5 py-3">
        <div className="font-mono text-[11px] uppercase tracking-widest text-muted-foreground">
          Backtest · RSI Low Revert · 1m
        </div>
        <span className="inline-flex items-center gap-1.5 rounded-full border border-border px-2.5 py-0.5 text-[10px] font-medium uppercase tracking-wider text-muted-foreground">
          <Share2 className="h-3 w-3" /> Shared report
        </span>
      </div>

      <div className="px-5 pt-4">
        <div className="flex flex-wrap items-baseline gap-x-4 gap-y-1">
          <span
            className="font-mono text-4xl font-semibold tabular-nums"
            style={{ color: 'var(--chart-gain)' }}
          >
            +168.4%
          </span>
          <span className="text-sm text-muted-foreground">
            $10,000 <ArrowRight className="inline h-3 w-3" /> $26,840
          </span>
        </div>
        <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1.5">
          <LegendDot color="var(--chart-strategy)" label="Strategy" />
          <LegendDot color="var(--chart-benchmark)" dashed label="SPY benchmark" />
          <LegendDot color="var(--chart-ceiling)" label="Max potential" />
        </div>
      </div>

      <div className="h-48 px-1 pt-3 sm:h-56">
        <EquityCurve />
      </div>

      <div className="grid grid-cols-2 gap-2 border-t border-border/80 p-3 sm:grid-cols-4">
        <KpiTile label="Profit factor" value="2.24" sub="avg win / avg loss" />
        <KpiTile label="Sharpe" value="6.72" sub="daily returns" />
        <KpiTile label="Win rate" value="58.9%" sub="512 of 540 signals" />
        <KpiTile label="Max drawdown" value="−6.8%" sub="peak to trough" />
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ */
/* Sections                                                            */
/* ------------------------------------------------------------------ */

function LandingNav() {
  return (
    <header className="fixed inset-x-0 top-0 z-50 border-b border-border/60 bg-background/80 backdrop-blur supports-[backdrop-filter]:bg-background/70">
      <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-3 sm:px-6">
        <Link to="/" className="text-foreground">
          <Wordmark />
        </Link>
        <nav className="hidden items-center gap-6 text-sm text-muted-foreground md:flex">
          <a href="#how" className="transition-colors hover:text-foreground">
            How it works
          </a>
          <a href="#features" className="transition-colors hover:text-foreground">
            Features
          </a>
          <a href="#pricing" className="transition-colors hover:text-foreground">
            Pricing
          </a>
          <a href="#faq" className="transition-colors hover:text-foreground">
            FAQ
          </a>
        </nav>
        <div className="flex items-center gap-2">
          <Link
            to="/sign-in"
            className="rounded-lg px-3 py-2 text-sm font-medium text-muted-foreground transition-colors hover:bg-accent hover:text-foreground"
          >
            Sign in
          </Link>
          <Link
            to="/sign-up"
            className="rounded-lg bg-primary px-3.5 py-2 text-sm font-medium text-primary-foreground transition-colors hover:bg-primary/90"
          >
            Start free
          </Link>
        </div>
      </div>
    </header>
  );
}

function Hero() {
  return (
    <section className="relative overflow-hidden pt-28 md:pt-36">
      <div className="landing-grid-bg pointer-events-none absolute inset-0" aria-hidden />
      <CandleSticks direction="down" className="left-[-70px] top-36 h-[440px] w-[250px]" />
      <CandleSticks className="right-[-50px] top-16 h-[560px] w-[300px]" />
      <div className="relative mx-auto max-w-6xl px-4 sm:px-6">
        <div className="mx-auto max-w-3xl text-center">
          <div className="landing-fade-up" style={{ animationDelay: '0ms' }}>
            <div className="mb-4 inline-flex items-center gap-2 rounded-full border border-border bg-card px-3 py-1 font-mono text-[11px] uppercase tracking-widest text-muted-foreground">
              Backtesting · Trading bots · Charts
            </div>
          </div>
          <h1
            className="landing-fade-up text-4xl font-semibold tracking-tight text-foreground sm:text-5xl md:text-6xl"
            style={{ animationDelay: '80ms' }}
          >
            Prove your edge before
            <br className="hidden sm:block" /> you risk a dollar.
          </h1>
          <p
            className="landing-fade-up mx-auto mt-5 max-w-2xl text-base text-muted-foreground sm:text-lg"
            style={{ animationDelay: '160ms' }}
          >
            StockMountain is a quant research desk for retail traders. Write strategies in a
            readable filter language, backtest them against years of minute-level market data, and
            put them on autopilot with paper-trading bots — so the numbers earn your trust before
            your money is on the line.
          </p>
          <div
            className="landing-fade-up mt-8 flex flex-wrap items-center justify-center gap-3"
            style={{ animationDelay: '240ms' }}
          >
            <Link
              to="/sign-up"
              className="inline-flex items-center gap-2 rounded-lg bg-primary px-5 py-3 text-sm font-semibold text-primary-foreground transition-all hover:bg-primary/90 active:translate-y-[0.5px]"
            >
              Start backtesting free <ArrowRight className="h-4 w-4" />
            </Link>
            <Link
              to="/strategies/community"
              className="inline-flex items-center gap-2 rounded-lg border border-border bg-card px-5 py-3 text-sm font-medium text-foreground transition-colors hover:bg-accent"
            >
              Explore community strategies
            </Link>
          </div>
          <p
            className="landing-fade-up mt-3 text-xs text-muted-foreground"
            style={{ animationDelay: '300ms' }}
          >
            Free tier included. No credit card required.
          </p>
        </div>

        <div className="landing-fade-up mx-auto mt-12 max-w-4xl md:mt-16" style={{ animationDelay: '380ms' }}>
          <ReportMock />
          <p className="mt-3 text-center text-xs text-muted-foreground">
            An example backtest report. Past performance does not guarantee future results.
          </p>
        </div>
      </div>
    </section>
  );
}

function HowItWorks() {
  return (
    <section id="how" className="mx-auto max-w-6xl scroll-mt-24 px-4 py-20 sm:px-6 md:py-28">
      <Reveal>
        <Eyebrow>How it works</Eyebrow>
        <h2 className="max-w-xl text-3xl font-semibold tracking-tight text-foreground md:text-4xl">
          From idea to autopilot in three steps.
        </h2>
      </Reveal>

      <div className="mt-12 grid gap-4 md:grid-cols-3">
        <Reveal delay={0}>
          <div className="flex h-full flex-col rounded-xl border border-border/80 bg-card p-6">
            <div className="font-mono text-xs text-muted-foreground">01</div>
            <h3 className="mt-2 text-lg font-semibold tracking-tight text-foreground">
              Write it like you'd say it
            </h3>
            <p className="mt-2 text-sm text-muted-foreground">
              Compose entry rules in a readable filter language — with autocomplete, instant
              validation, and a plain-English readback of exactly what you wrote.
            </p>
            <div className="mt-5 flex flex-wrap gap-2">
              <FilterChip>rsi(14) &lt; 30 [1m]</FilterChip>
              <FilterChip>price &gt; sma(50) [1d]</FilterChip>
              <FilterChip>volume &gt; 1,000,000</FilterChip>
            </div>
            <div className="mt-3 flex items-start gap-1.5 text-xs text-muted-foreground">
              <Check className="mt-0.5 h-3.5 w-3.5 shrink-0" style={{ color: 'var(--chart-gain)' }} />
              <span>"RSI(14) on 1-minute bars is below 30"</span>
            </div>
          </div>
        </Reveal>

        <Reveal delay={100}>
          <div className="flex h-full flex-col rounded-xl border border-border/80 bg-card p-6">
            <div className="font-mono text-xs text-muted-foreground">02</div>
            <h3 className="mt-2 text-lg font-semibold tracking-tight text-foreground">
              Backtest against real history
            </h3>
            <p className="mt-2 text-sm text-muted-foreground">
              Run it over years of minute-level data and get a full research report — equity curve
              vs. SPY, drawdowns, exit efficiency, and where the edge actually lives.
            </p>
            <div className="mt-5 grid grid-cols-2 gap-2">
              <KpiTile label="Expectancy" value="+$41" sub="per trade" />
              <KpiTile label="Exit efficiency" value="72%" sub="of max potential" />
              <KpiTile label="Median hold" value="38m" />
              <KpiTile label="Coverage" value="512/540" sub="signals taken" />
            </div>
          </div>
        </Reveal>

        <Reveal delay={200}>
          <div className="flex h-full flex-col rounded-xl border border-border/80 bg-card p-6">
            <div className="font-mono text-xs text-muted-foreground">03</div>
            <h3 className="mt-2 text-lg font-semibold tracking-tight text-foreground">
              Let a bot fly it on paper
            </h3>
            <p className="mt-2 text-sm text-muted-foreground">
              Deploy the same rules your backtest proved — identical fill semantics — and watch it
              trade live markets without risking a cent.
            </p>
            <div className="mt-5 space-y-2">
              <div className="flex items-center gap-3 rounded-lg border border-border bg-muted/40 px-3 py-2.5 text-sm">
                <FlaskConical className="h-4 w-4 text-muted-foreground" />
                <span className="text-foreground">Simulator</span>
                <span className="ml-auto text-xs text-muted-foreground">deterministic fills</span>
              </div>
              <div className="flex items-center gap-3 rounded-lg border border-border bg-muted/40 px-3 py-2.5 text-sm">
                <Bot className="h-4 w-4 text-muted-foreground" />
                <span className="text-foreground">Broker paper account</span>
                <span className="ml-auto text-xs text-muted-foreground">real market, fake money</span>
              </div>
              <div className="flex items-center gap-3 rounded-lg border border-dashed border-border px-3 py-2.5 text-sm">
                <Lock className="h-4 w-4 text-muted-foreground" />
                <span className="text-muted-foreground">Live trading</span>
                <span className="ml-auto text-xs text-muted-foreground">when the numbers earn it</span>
              </div>
            </div>
          </div>
        </Reveal>
      </div>
    </section>
  );
}

const FEATURES = [
  {
    icon: BarChart3,
    title: 'Research-grade backtest reports',
    body: 'Equity curve vs. SPY and a max-potential ceiling, drawdown view, P&L by entry time and exit reason, trade distributions, ticker leaderboards — every trade accounted for.',
  },
  {
    icon: SlidersHorizontal,
    title: 'A strategy language, not a form',
    body: 'Multi-timeframe expressions like macd(12,26,9).histogram > 0 [5m] with validation, English readback, and a reusable filter library.',
  },
  {
    icon: Bot,
    title: 'Paper trading bots',
    body: 'Strategies run as bots with live balance history, a trade calendar, and a full trade log. Backtest and bot share the same fill model, so paper results mean something.',
  },
  {
    icon: CandlestickChart,
    title: 'Multi-chart workspace',
    body: 'A draggable grid of real-time candlestick charts with SMA, EMA, MACD, RSI and more — your watchlist on one screen.',
  },
  {
    icon: FlaskConical,
    title: 'Strategy optimizer',
    body: 'Replay filter variants side by side — trades, P&L, and win rate per variant — to tune parameters with evidence instead of vibes.',
  },
  {
    icon: EyeOff,
    title: 'Share results, not your rules',
    body: 'Publish a live report link with the strategy config masked. Prove the performance while your entry logic stays yours.',
    extra: (
      <div className="mt-4 flex flex-wrap items-center gap-2">
        <FilterChip dim>rsi(14) &lt; 30 [1m]</FilterChip>
        <FilterChip dim>price &gt; vwap()</FilterChip>
        <span className="text-[11px] text-muted-foreground">3 entry filters · stop loss · timed exit</span>
      </div>
    ),
  },
] as const;

function Features() {
  return (
    <section id="features" className="relative overflow-hidden border-y border-border/60 bg-card/40">
      <CandleSticks className="left-[-60px] top-24 h-[520px] w-[280px]" />
      <div className="relative mx-auto max-w-6xl scroll-mt-24 px-4 py-20 sm:px-6 md:py-28">
        <Reveal>
          <Eyebrow>Features</Eyebrow>
          <h2 className="max-w-xl text-3xl font-semibold tracking-tight text-foreground md:text-4xl">
            The whole research desk.
          </h2>
          <p className="mt-3 max-w-xl text-sm text-muted-foreground sm:text-base">
            Everything between "I have a hunch" and "I have a system" — plus a stock scanner in
            early access.
          </p>
        </Reveal>

        <div className="mt-12 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {FEATURES.map(({ icon: Icon, title, body, ...rest }, i) => (
            <Reveal key={title} delay={(i % 3) * 100}>
              <div className="h-full rounded-xl border border-border/80 bg-card p-6">
                <div className="mb-3 inline-flex rounded-lg border border-border bg-muted/60 p-2 text-muted-foreground">
                  <Icon className="h-5 w-5" />
                </div>
                <h3 className="text-base font-semibold tracking-tight text-foreground">{title}</h3>
                <p className="mt-2 text-sm text-muted-foreground">{body}</p>
                {'extra' in rest ? rest.extra : null}
              </div>
            </Reveal>
          ))}
        </div>
      </div>
    </section>
  );
}

/* ------------------------------------------------------------------ */
/* Pricing                                                             */
/* ------------------------------------------------------------------ */

type Plan = {
  name: string;
  price: string;
  period?: string;
  /** Annual option: yearly price ÷ 12 (~20% off) plus a one-month bonus-credit grant. */
  annual?: { perMonth: string; perYear: string; bonusCredits: string };
  tagline: string;
  cta: string;
  highlighted?: boolean;
  features: string[];
};

const PLANS: Plan[] = [
  {
    name: 'Free',
    price: '$0',
    tagline: 'Kick the tires. Keep the charts.',
    cta: 'Start free',
    features: [
      '100 backtest credits / month',
      'Full charting workspace + indicators',
      '1 paper trading bot (simulator)',
      'Community strategy dashboard',
      'Shareable backtest reports',
    ],
  },
  {
    name: 'Pro',
    price: '$29',
    period: '/mo',
    annual: { perMonth: '$23.25', perYear: '$279', bonusCredits: '1,000' },
    tagline: 'For traders building a real playbook.',
    cta: 'Start with Pro',
    highlighted: true,
    features: [
      '1,000 backtest credits / month',
      '10 paper trading bots — simulator + broker paper',
      'Strategy optimizer',
      'Stock scanner (early access)',
      'Masked sharing — publish results, hide your rules',
      'Filter library & templates',
    ],
  },
  {
    name: 'Premium',
    price: '$99',
    period: '/mo',
    annual: { perMonth: '$79.08', perYear: '$949', bonusCredits: '5,000' },
    tagline: 'For traders ready to go live.',
    cta: 'Go Premium',
    features: [
      '5,000 backtest credits / month',
      'Unlimited paper trading bots',
      'Live trading — early access',
      'Priority backtest queue',
      'Priority support',
      'Everything in Pro',
    ],
  },
];

function Pricing() {
  // Signed-in users go to /billing (where checkout lives) instead of sign-up;
  // the selected cycle rides along so they don't have to re-pick Annual there.
  const { isSignedIn } = useUser();
  const [annual, setAnnual] = useState(false);
  const ctaTarget = isSignedIn ? (annual ? '/billing?cycle=annual' : '/billing') : '/sign-up';
  return (
    <section id="pricing" className="relative scroll-mt-24 overflow-hidden">
      <CandleSticks className="right-[-70px] top-32 h-[560px] w-[300px]" />
      <div className="relative mx-auto max-w-6xl px-4 py-20 sm:px-6 md:py-28">
      <Reveal className="text-center">
        <Eyebrow>Pricing</Eyebrow>
        <h2 className="mx-auto max-w-2xl text-3xl font-semibold tracking-tight text-foreground md:text-4xl">
          Pay for compute, not promises.
        </h2>
        <p className="mx-auto mt-3 max-w-xl text-sm text-muted-foreground sm:text-base">
          No courses. No signal groups. Just the tools to test your own ideas — priced like
          software.
        </p>
        <div className="mt-6 inline-flex items-center rounded-lg border border-border bg-card p-0.5 text-sm">
          {([false, true] as const).map((isAnnual) => (
            <button
              key={String(isAnnual)}
              onClick={() => setAnnual(isAnnual)}
              aria-pressed={annual === isAnnual}
              className={cn(
                'rounded-md px-4 py-1.5 font-medium transition-colors',
                annual === isAnnual
                  ? 'bg-primary text-primary-foreground'
                  : 'text-muted-foreground hover:text-foreground',
              )}
            >
              {isAnnual ? 'Annual · 20% off' : 'Monthly'}
            </button>
          ))}
        </div>
      </Reveal>

      <div className="mt-12 grid gap-4 lg:grid-cols-3">
        {PLANS.map((plan, i) => (
          <Reveal key={plan.name} delay={i * 100}>
            <div
              className={cn(
                'relative flex h-full flex-col rounded-xl border bg-card p-6',
                plan.highlighted
                  ? 'border-foreground/40 shadow-xl shadow-black/10'
                  : 'border-border/80',
              )}
            >
              {plan.highlighted && (
                <span className="absolute -top-3 left-1/2 -translate-x-1/2 rounded-full bg-primary px-3 py-0.5 text-[10px] font-semibold uppercase tracking-wider text-primary-foreground">
                  Most popular
                </span>
              )}
              <div className="font-mono text-[11px] uppercase tracking-widest text-muted-foreground">
                {plan.name}
              </div>
              <div className="mt-2 flex items-baseline gap-1">
                <span className="font-mono text-4xl font-semibold tabular-nums text-foreground">
                  {annual && plan.annual ? plan.annual.perMonth : plan.price}
                </span>
                {(plan.period || (annual && plan.annual)) && (
                  <span className="text-sm text-muted-foreground">/mo</span>
                )}
              </div>
              {annual && plan.annual && (
                <>
                  <p className="mt-1 text-xs text-muted-foreground">
                    billed annually ({plan.annual.perYear}/yr)
                  </p>
                  <span
                    className="mt-2 inline-flex w-fit rounded-full px-2.5 py-0.5 text-[10px] font-semibold uppercase tracking-wider"
                    style={{ color: 'var(--chart-gain)', backgroundColor: 'color-mix(in srgb, var(--chart-gain) 12%, transparent)' }}
                  >
                    20% off + {plan.annual.bonusCredits} bonus credits
                  </span>
                </>
              )}
              <p className="mt-1.5 text-sm text-muted-foreground">{plan.tagline}</p>
              <ul className="mt-5 flex-1 space-y-2.5">
                {plan.features.map((f) => (
                  <li key={f} className="flex items-start gap-2 text-sm text-foreground">
                    <Check
                      className="mt-0.5 h-4 w-4 shrink-0"
                      style={{ color: 'var(--chart-gain)' }}
                    />
                    <span>{f}</span>
                  </li>
                ))}
              </ul>
              <Link
                to={ctaTarget}
                className={cn(
                  'mt-6 inline-flex items-center justify-center rounded-lg px-4 py-2.5 text-sm font-semibold transition-colors',
                  plan.highlighted
                    ? 'bg-primary text-primary-foreground hover:bg-primary/90'
                    : 'border border-border bg-card text-foreground hover:bg-accent',
                )}
              >
                {plan.cta}
              </Link>
            </div>
          </Reveal>
        ))}
      </div>

      <Reveal>
        <p className="mx-auto mt-6 max-w-2xl text-center text-xs text-muted-foreground">
          Launch pricing — cancel anytime and keep access through the period you've paid for.
          Monthly credits reset each month on every plan; one-time credit packs and annual
          bonus credits never expire. Live trading is rolling out gradually to Premium members
          behind additional safety checks; every strategy climbs simulator → paper → live.
        </p>
      </Reveal>
      </div>
    </section>
  );
}

/* ------------------------------------------------------------------ */
/* FAQ + CTA + footer                                                  */
/* ------------------------------------------------------------------ */

const FAQS = [
  {
    q: 'What is a backtest credit?',
    a: 'Backtests are metered by the compute they use — bigger universes, finer timeframes, and longer date ranges cost more credits. A typical single-strategy test costs a few dozen credits, and your allowance resets every month.',
  },
  {
    q: 'Is my strategy private?',
    a: 'Yes — strategies and backtests are private by default. When you share a report, you choose whether the strategy config is visible or masked; masked links show your performance and exits without revealing your entry logic.',
  },
  {
    q: 'When can I trade real money?',
    a: 'Live trading is in early access for Premium members and rolling out deliberately. Every strategy climbs the same ladder — simulator, then a broker paper account, then live — because we would rather you prove it first than fund it first.',
  },
  {
    q: 'What data powers the backtests?',
    a: 'Minute-resolution US equities history from an institutional market data provider — the same data your bots trade against, so backtest and paper results stay comparable.',
  },
  {
    q: 'Is this financial advice?',
    a: 'No. StockMountain is research and analytics software. We give you the instruments to test your own ideas; what you trade is up to you, and past performance never guarantees future results.',
  },
  {
    q: 'Can I cancel anytime?',
    a: 'Yes. Cancelling stops future charges and you keep full access until the end of the period you already paid for — the rest of the month on monthly plans, the rest of the year on annual plans. We don\'t issue automatic refunds for unused time. Downgrading adjusts your credit allowance and bot limits at the next cycle.',
  },
  {
    q: 'How does annual billing work?',
    a: 'Annual plans are the same product at 20% off, billed once a year, plus a one-time bonus of a full month\'s credits added to your never-expiring purchased balance at signup and again at each renewal. Your monthly credit allowance still resets every month. You can switch between monthly and annual in the billing portal.',
  },
];

function Faq() {
  return (
    <section id="faq" className="border-t border-border/60 bg-card/40">
      <div className="mx-auto max-w-6xl scroll-mt-24 px-4 py-20 sm:px-6 md:py-28">
        <Reveal>
          <Eyebrow>FAQ</Eyebrow>
          <h2 className="text-3xl font-semibold tracking-tight text-foreground md:text-4xl">
            Questions, answered.
          </h2>
        </Reveal>
        <div className="mt-10 grid gap-x-10 gap-y-8 md:grid-cols-2">
          {FAQS.map(({ q, a }, i) => (
            <Reveal key={q} delay={(i % 2) * 100}>
              <h3 className="text-sm font-semibold text-foreground">{q}</h3>
              <p className="mt-1.5 text-sm text-muted-foreground">{a}</p>
            </Reveal>
          ))}
        </div>
      </div>
    </section>
  );
}

function FinalCta() {
  return (
    <section className="relative overflow-hidden">
      <div className="landing-grid-bg pointer-events-none absolute inset-0 rotate-180" aria-hidden />
      <CandleSticks direction="down" className="left-[-60px] top-8 h-[420px] w-[260px]" />
      <CandleSticks className="right-[-60px] top-0 h-[460px] w-[280px]" />
      <div className="relative mx-auto max-w-6xl px-4 py-20 text-center sm:px-6 md:py-28">
        <Reveal>
          <h2 className="mx-auto max-w-2xl text-3xl font-semibold tracking-tight text-foreground md:text-4xl">
            The market doesn't care what you believe.
            <br />
            Bring numbers.
          </h2>
          <div className="mt-8">
            <Link
              to="/sign-up"
              className="inline-flex items-center gap-2 rounded-lg bg-primary px-6 py-3 text-sm font-semibold text-primary-foreground transition-all hover:bg-primary/90 active:translate-y-[0.5px]"
            >
              Start backtesting free <ArrowRight className="h-4 w-4" />
            </Link>
          </div>
        </Reveal>
      </div>
    </section>
  );
}

function Footer() {
  return (
    <footer className="border-t border-border/60">
      <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
        <div className="flex flex-col items-start justify-between gap-6 sm:flex-row sm:items-center">
          <Wordmark className="text-foreground" />
          <nav className="flex flex-wrap gap-x-6 gap-y-2 text-sm text-muted-foreground">
            <a href="#pricing" className="transition-colors hover:text-foreground">
              Pricing
            </a>
            <Link to="/strategies/community" className="transition-colors hover:text-foreground">
              Community strategies
            </Link>
            <Link to="/sign-in" className="transition-colors hover:text-foreground">
              Sign in
            </Link>
          </nav>
        </div>
        <p className="mt-8 max-w-3xl text-xs leading-relaxed text-muted-foreground">
          StockMountain is analytics software, not a broker or investment adviser. Nothing here is
          financial advice. Backtested and simulated results are hypothetical, do not reflect
          actual trading, and past performance does not guarantee future results. Trading involves
          substantial risk of loss.
        </p>
        <p className="mt-4 text-xs text-muted-foreground">
          © {new Date().getFullYear()} StockMountain
        </p>
      </div>
    </footer>
  );
}

/* ------------------------------------------------------------------ */

export function LandingPage() {
  return (
    <div className="min-h-screen bg-background text-foreground">
      <LandingNav />
      <main>
        <Hero />
        <HowItWorks />
        <Features />
        <Pricing />
        <Faq />
        <FinalCta />
      </main>
      <Footer />
    </div>
  );
}
