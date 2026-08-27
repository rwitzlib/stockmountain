interface SectionHeadingProps {
  index: string;
  label: string;
  title: string;
  hint?: string;
}

/** Numbered card heading shared by the strategy, backtest, and scanner create pages. */
export function SectionHeading({ index, label, title, hint }: SectionHeadingProps) {
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
