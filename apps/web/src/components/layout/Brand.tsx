interface BrandMarkProps {
  className?: string;
}

/** The StockMountain mark — same artwork as public/favicon.svg. */
export function BrandMark({ className }: BrandMarkProps) {
  return (
    <svg viewBox="0 0 64 64" className={className} aria-hidden="true">
      <rect width="64" height="64" rx="14" fill="#14171C" />
      <g
        fill="none"
        stroke="#F2F4F7"
        strokeWidth="4"
        strokeLinecap="round"
        strokeLinejoin="round"
      >
        <path d="M12 48 L25 24 L33 36 L43 16 L52 48" />
        <path d="M12 48 H52" opacity="0.35" />
      </g>
    </svg>
  );
}

interface BrandProps {
  /** Hide the wordmark, showing only the mark (collapsed sidebar) */
  markOnly?: boolean;
}

export function Brand({ markOnly = false }: BrandProps) {
  return (
    <span className="flex items-center gap-2.5">
      <BrandMark className="h-7 w-7 shrink-0 rounded-lg ring-1 ring-border" />
      {!markOnly && (
        <span className="text-sm font-semibold tracking-tight text-foreground">
          StockMountain
        </span>
      )}
    </span>
  );
}
