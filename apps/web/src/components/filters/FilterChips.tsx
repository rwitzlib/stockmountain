import { useMemo, useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { filtersApi } from '../../api/filtersApi';
import type { FilterSegment, FilterSegmentRole } from '../../types/filters';

interface FilterChipsProps {
  expression: string;
  /**
   * When provided, segment clicks let the user quick-edit that piece. An edit splices the new
   * token into the canonical text, re-validates it, and reports the server's canonical result.
   */
  onChange?: (expression: string) => void;
  className?: string;
}

/** A slice of the canonical text: a segment (colored, maybe editable) or the punctuation between two. */
interface Piece {
  text: string;
  segment?: FilterSegment;
}

const ROLE_CLASS: Record<FilterSegmentRole, string> = {
  function: 'text-sky-700 dark:text-sky-400',
  data: 'text-foreground',
  literal: 'text-amber-700 dark:text-amber-400',
  op: 'text-muted-foreground font-semibold',
  logic: 'text-purple-700 dark:text-purple-400 font-semibold',
  timeframe: 'text-green-700 dark:text-green-400',
};

const OPERATORS = ['>', '<', '>=', '<=', '=', '!='];
// Fallbacks only; the live choices come from the /filters/functions suffix entry.
const TIMEFRAMES = ['1m', '5m', '15m', '30m', '1h', '1d', '1w'];
const MODES = ['all', 'any'];

/** Splits the canonical text into segments and the punctuation between them, in order. */
function toPieces(canonical: string, segments: FilterSegment[]): Piece[] {
  const pieces: Piece[] = [];
  let cursor = 0;
  for (const segment of segments) {
    if (segment.start > cursor) pieces.push({ text: canonical.slice(cursor, segment.start) });
    pieces.push({ text: canonical.slice(segment.start, segment.end), segment });
    cursor = segment.end;
  }
  if (cursor < canonical.length) pieces.push({ text: canonical.slice(cursor) });
  return pieces;
}

export function FilterChips({ expression, onChange, className }: FilterChipsProps) {
  const [editingIndex, setEditingIndex] = useState<number | null>(null);
  const [draft, setDraft] = useState('');
  const [editError, setEditError] = useState<string | null>(null);
  // Only the newest edit may commit: a slower validation of an earlier edit must not overwrite it.
  const editSeq = useRef(0);

  const { data: results } = useQuery({
    queryKey: ['filterAst', expression],
    queryFn: () => filtersApi.validate([expression]),
    staleTime: Infinity,
    retry: 1,
  });

  // Suffix choices (timeframes, modes) are catalog-driven, like the composer's bracket hint.
  const { data: functions = [] } = useQuery({
    queryKey: ['filterFunctions', 'all'],
    queryFn: () => filtersApi.getFunctions(),
    staleTime: Infinity,
    retry: 1,
    enabled: !!onChange,
  });
  const suffixOptions = functions.find((f) => f.kind === 'suffix')?.paramOptions;

  const result = results?.[0];
  const canonical = result?.valid ? result.canonical : undefined;
  const segments = result?.valid ? result.segments : undefined;

  const pieces = useMemo(
    () => (canonical && segments ? toPieces(canonical, segments) : null),
    [canonical, segments],
  );

  // Unparseable (legacy) or still loading: fall back to plain text
  if (!pieces || !canonical) {
    return (
      <code className={className ?? 'rounded-md border border-border/60 bg-muted/50 px-2.5 py-1.5 font-mono text-xs'}>
        {expression}
      </code>
    );
  }

  const optionsFor = (segment: FilterSegment): string[] | null => {
    switch (segment.edit) {
      case 'op':
        return OPERATORS;
      case 'timeframe':
        return suffixOptions?.timeframe ?? TIMEFRAMES;
      case 'mode':
        return suffixOptions?.mode ?? MODES;
      default:
        return null;
    }
  };

  const commitEdit = async (segment: FilterSegment, raw: string) => {
    setEditingIndex(null);
    const value = raw.trim();
    if (!value || !onChange) return;
    if (segment.edit === 'op' && !OPERATORS.includes(value)) return;

    // The edit is a splice on the segment's span; the server decides what it means.
    const spliced = canonical.slice(0, segment.start) + value + canonical.slice(segment.end);
    if (spliced === canonical) return;

    const seq = ++editSeq.current;
    try {
      const [next] = await filtersApi.validate([spliced]);
      if (seq !== editSeq.current) return; // a newer edit superseded this one
      if (!next?.valid) {
        setEditError(next?.error ?? 'That edit does not parse.');
        return;
      }
      setEditError(null);
      // The server's canonical spelling wins; a valid response without one keeps the edit as spliced.
      const committed = next.canonical ?? spliced;
      if (committed !== canonical) onChange(committed);
    } catch {
      if (seq !== editSeq.current) return;
      // Validation endpoint unreachable: hand the spliced text on rather than blocking the edit, but say so.
      setEditError('Validation unavailable. The edit was applied unchecked.');
      onChange(spliced);
    }
  };

  return (
    <span className={`inline-flex flex-col items-start gap-0.5 ${className ?? ''}`}>
      <span className="inline-flex flex-wrap items-baseline font-mono text-xs">
        {pieces.map((piece, i) => {
          const key = `${i}-${piece.text}`;
          const segment = piece.segment;
          if (!segment) {
            // Punctuation and spacing straight from the canonical text.
            return (
              <span key={key} className="whitespace-pre text-muted-foreground">
                {piece.text}
              </span>
            );
          }

          const editable = !!onChange && !!segment.edit;
          if (editingIndex === i && editable) {
            const options = optionsFor(segment);
            if (options) {
              const choices = options.includes(piece.text) ? options : [piece.text, ...options];
              return (
                <select
                  key={key}
                  autoFocus
                  onClick={(e) => e.stopPropagation()}
                  defaultValue={piece.text}
                  onChange={(e) => commitEdit(segment, e.target.value)}
                  onBlur={() => setEditingIndex(null)}
                  className="h-6 rounded border border-input bg-card px-1 font-mono text-xs"
                >
                  {choices.map((o) => (
                    <option key={o} value={o}>{o}</option>
                  ))}
                </select>
              );
            }
            return (
              <input
                key={key}
                autoFocus
                inputMode={segment.edit === 'candles' ? 'numeric' : 'decimal'}
                onClick={(e) => e.stopPropagation()}
                value={draft}
                onChange={(e) => setDraft(e.target.value)}
                onBlur={() => commitEdit(segment, draft)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') commitEdit(segment, draft);
                  if (e.key === 'Escape') setEditingIndex(null);
                }}
                className="h-6 w-16 rounded border border-input bg-card px-1 font-mono text-xs"
              />
            );
          }

          if (!editable) {
            // Plain span so clicks bubble to whatever container owns the row (e.g. open full editor).
            return (
              <span key={key} className={`rounded px-0.5 ${ROLE_CLASS[segment.role]}`}>
                {piece.text}
              </span>
            );
          }

          return (
            <button
              key={key}
              type="button"
              title={`Quick edit ${segment.edit}`}
              onClick={(e) => {
                e.stopPropagation();
                setDraft(piece.text);
                setEditError(null);
                setEditingIndex(i);
              }}
              className={`rounded px-0.5 ${ROLE_CLASS[segment.role]} cursor-pointer hover:bg-accent hover:underline decoration-dotted underline-offset-2`}
            >
              {piece.text}
            </button>
          );
        })}
      </span>
      {editError && (
        <span role="alert" className="text-[11px] text-red-600 dark:text-red-400">
          {editError}
        </span>
      )}
    </span>
  );
}
