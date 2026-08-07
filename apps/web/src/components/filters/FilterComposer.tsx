import {
  forwardRef,
  useEffect,
  useImperativeHandle,
  useMemo,
  useRef,
  useState,
  type Ref,
} from 'react';
import { useQuery } from '@tanstack/react-query';
import { AlertCircle, Check, Loader2 } from 'lucide-react';
import { Button } from '../ui/button';
import { filtersApi } from '../../api/filtersApi';
import {
  FILTER_TEMPLATES,
  loadFilterLibrary,
  recordFilterUse,
} from './filterExpression';
import type { FilterFunctionInfo, FilterValidationResult } from '../../types/filters';

export interface FilterComposerRef {
  setExpression: (expression: string) => void;
}

interface FilterComposerProps {
  onAddFilter: (expression: string) => void;
  addButtonLabel?: string;
  disabled?: boolean;
}

interface Suggestion {
  label: string;
  detail: string;
  insert: string;
  /** Replace the current word (autocomplete) vs. the whole input (recents/templates). */
  replaceWord: boolean;
}

const wordAt = (text: string, caret: number) => {
  const left = text.slice(0, caret);
  const match = left.match(/[a-zA-Z_]+$/);
  return { word: match?.[0] ?? '', start: caret - (match?.[0].length ?? 0) };
};

/** The innermost unclosed function call before the caret, for signature hints. */
const enclosingFunction = (text: string, caret: number): { name: string; parenIndex: number } | null => {
  let depth = 0;
  for (let i = caret - 1; i >= 0; i--) {
    const ch = text[i];
    if (ch === ')') depth++;
    if (ch === '(') {
      if (depth === 0) {
        const match = text.slice(0, i).match(/([a-zA-Z_]+)$/);
        return match ? { name: match[1], parenIndex: i } : null;
      }
      depth--;
    }
  }
  return null;
};

/** Which argument the caret sits in: top-level commas between the open paren and the caret. */
const argIndexAt = (text: string, parenIndex: number, caret: number): number => {
  let depth = 0;
  let index = 0;
  for (let i = parenIndex + 1; i < caret; i++) {
    const ch = text[i];
    if (ch === '(') depth++;
    if (ch === ')') depth--;
    if (ch === ',' && depth === 0) index++;
  }
  return index;
};

export const FilterComposer = forwardRef(function FilterComposer(
  { onAddFilter, addButtonLabel = 'Add Filter', disabled = false }: FilterComposerProps,
  ref: Ref<FilterComposerRef>
) {
  const [input, setInput] = useState('');
  const [caret, setCaret] = useState(0);
  const [menuOpen, setMenuOpen] = useState(false);
  const [menuIndex, setMenuIndex] = useState(0);
  const [validation, setValidation] = useState<FilterValidationResult | null>(null);
  const [validating, setValidating] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const validateSeq = useRef(0);

  useImperativeHandle(ref, () => ({
    setExpression: (expression: string) => {
      setInput(expression);
      inputRef.current?.focus();
    },
  }));

  const { data: functions = [] } = useQuery({
    queryKey: ['filterFunctions'],
    queryFn: filtersApi.getFunctions,
    staleTime: Infinity,
    retry: 1,
  });

  // Debounced validation against the real parser
  useEffect(() => {
    const expression = input.trim();
    if (!expression) {
      setValidation(null);
      setValidating(false);
      return;
    }
    setValidating(true);
    const seq = ++validateSeq.current;
    const timer = setTimeout(async () => {
      try {
        const [result] = await filtersApi.validate([expression]);
        if (seq === validateSeq.current) setValidation(result ?? null);
      } catch {
        if (seq === validateSeq.current) setValidation(null); // API unreachable — don't block
      } finally {
        if (seq === validateSeq.current) setValidating(false);
      }
    }, 300);
    return () => clearTimeout(timer);
  }, [input]);

  const { word, start: wordStart } = wordAt(input, caret);

  const suggestions = useMemo<Suggestion[]>(() => {
    if (!input.trim()) {
      const library = loadFilterLibrary();
      const pinned = library.filter((e) => e.pinned).slice(0, 5);
      const recent = library.filter((e) => !e.pinned).slice(0, 5);
      return [
        ...pinned.map((e) => ({ label: e.expression, detail: 'Pinned', insert: e.expression, replaceWord: false })),
        ...recent.map((e) => ({ label: e.expression, detail: 'Recent', insert: e.expression, replaceWord: false })),
        ...FILTER_TEMPLATES.map((t) => ({ label: t.expression, detail: t.label, insert: t.expression, replaceWord: false })),
      ];
    }
    if (word.length < 1) return [];
    return functions
      .filter((f: FilterFunctionInfo) => f.name.startsWith(word.toLowerCase()) && f.name !== word)
      .slice(0, 8)
      .map((f) => ({ label: f.signature, detail: f.description, insert: f.snippet, replaceWord: true }));
  }, [functions, input, word]);

  const signatureHint = useMemo(() => {
    const enclosing = enclosingFunction(input, caret);
    if (!enclosing) return null;
    const fn = functions.find((f) => f.name === enclosing.name);
    if (!fn) return null;
    return { fn, activeArg: argIndexAt(input, enclosing.parenIndex, caret) };
  }, [functions, input, caret]);

  const applySuggestion = (suggestion: Suggestion) => {
    let next: string;
    let caretTarget: number;
    if (suggestion.replaceWord) {
      next = input.slice(0, wordStart) + suggestion.insert + input.slice(caret);
      caretTarget = wordStart + suggestion.insert.length;
    } else {
      next = suggestion.insert;
      caretTarget = next.length;
    }
    setInput(next);
    setMenuOpen(false);
    requestAnimationFrame(() => {
      const el = inputRef.current;
      if (!el) return;
      el.focus();
      // Select the first argument of an inserted snippet so typing replaces it
      const parenIdx = suggestion.replaceWord ? suggestion.insert.indexOf('(') : -1;
      if (parenIdx > 0 && suggestion.insert.length > parenIdx + 1 && suggestion.insert[parenIdx + 1] !== ')') {
        const argStart = wordStart + parenIdx + 1;
        const argEnd = wordStart + Math.min(
          ...[',', ')'].map((c) => {
            const idx = suggestion.insert.indexOf(c, parenIdx + 1);
            return idx === -1 ? suggestion.insert.length : idx;
          })
        );
        el.setSelectionRange(argStart, argEnd);
        setCaret(argEnd);
      } else {
        el.setSelectionRange(caretTarget, caretTarget);
        setCaret(caretTarget);
      }
    });
  };

  /** Tab inside parens: select the next argument segment. */
  const selectNextArg = (): boolean => {
    const el = inputRef.current;
    if (!el) return false;
    const from = el.selectionEnd ?? caret;
    let depth = 0;
    for (let i = from; i < input.length; i++) {
      const ch = input[i];
      if (ch === '(') depth++;
      if (ch === ')') {
        if (depth === 0) return false;
        depth--;
      }
      if (ch === ',' && depth === 0) {
        let s = i + 1;
        while (s < input.length && input[s] === ' ') s++;
        let e = s;
        let d = 0;
        while (e < input.length && !(d === 0 && (input[e] === ',' || input[e] === ')'))) {
          if (input[e] === '(') d++;
          if (input[e] === ')') d--;
          e++;
        }
        el.setSelectionRange(s, e);
        setCaret(e);
        return true;
      }
    }
    return false;
  };

  const handleAdd = async () => {
    const expression = input.trim();
    if (!expression || disabled) return;
    try {
      const [result] = await filtersApi.validate([expression]);
      if (!result?.valid) {
        setValidation(result ?? null);
        return;
      }
      setValidation(result);
    } catch {
      // Validation endpoint unreachable — allow authoring rather than blocking
    }
    recordFilterUse(expression);
    onAddFilter(expression);
    setInput('');
    setValidation(null);
    setMenuOpen(false);
  };

  const status = !input.trim()
    ? null
    : validating
      ? 'validating'
      : validation === null
        ? 'unknown'
        : validation.valid
          ? 'valid'
          : 'invalid';

  return (
    <div className="space-y-2">
      <div className="relative">
        <div className="flex items-center gap-2">
          <div className="relative flex-1">
            <input
              ref={inputRef}
              value={input}
              disabled={disabled}
              placeholder="Type a filter, e.g. rsi(14) < 30 [1m] — or pick a template"
              className="w-full rounded-lg border border-input bg-card text-foreground font-mono text-xs px-3 py-2 pr-8 placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring"
              onChange={(e) => {
                setInput(e.target.value);
                setCaret(e.target.selectionStart ?? e.target.value.length);
                setMenuOpen(true);
                setMenuIndex(0);
              }}
              onFocus={(e) => {
                setCaret(e.target.selectionStart ?? 0);
                setMenuOpen(true);
              }}
              onBlur={() => setTimeout(() => setMenuOpen(false), 150)}
              onSelect={(e) => setCaret((e.target as HTMLInputElement).selectionStart ?? 0)}
              onKeyDown={(e) => {
                if (menuOpen && suggestions.length > 0) {
                  if (e.key === 'ArrowDown') {
                    e.preventDefault();
                    setMenuIndex((i) => (i + 1) % suggestions.length);
                    return;
                  }
                  if (e.key === 'ArrowUp') {
                    e.preventDefault();
                    setMenuIndex((i) => (i - 1 + suggestions.length) % suggestions.length);
                    return;
                  }
                  if ((e.key === 'Enter' || e.key === 'Tab') && word.length > 0) {
                    e.preventDefault();
                    applySuggestion(suggestions[menuIndex]);
                    return;
                  }
                  if (e.key === 'Escape') {
                    e.preventDefault();
                    setMenuOpen(false);
                    return;
                  }
                }
                if (e.key === 'Tab' && selectNextArg()) {
                  e.preventDefault();
                  return;
                }
                if (e.key === 'Enter') {
                  e.preventDefault();
                  handleAdd();
                }
              }}
            />
            <span className="absolute right-2.5 top-1/2 -translate-y-1/2">
              {status === 'validating' && <Loader2 className="h-3.5 w-3.5 animate-spin text-muted-foreground" />}
              {status === 'valid' && <Check className="h-3.5 w-3.5 text-green-600 dark:text-green-400" />}
              {status === 'invalid' && <AlertCircle className="h-3.5 w-3.5 text-red-600 dark:text-red-400" />}
            </span>
          </div>
          <Button
            onClick={handleAdd}
            disabled={disabled || !input.trim() || status === 'invalid'}
            className="bg-primary text-primary-foreground hover:bg-primary/90 text-xs font-medium px-3 py-1 transition-colors disabled:opacity-50"
          >
            {addButtonLabel}
          </Button>
        </div>

        {menuOpen && suggestions.length > 0 && (
          <div className="absolute z-20 mt-1 w-full max-h-64 overflow-y-auto rounded-lg border border-border bg-popover shadow-md">
            {suggestions.map((s, i) => (
              <button
                key={`${s.label}-${i}`}
                type="button"
                onMouseDown={(e) => {
                  e.preventDefault();
                  applySuggestion(s);
                }}
                className={`flex w-full items-baseline justify-between gap-3 px-3 py-1.5 text-left ${
                  i === menuIndex ? 'bg-accent' : 'hover:bg-accent/60'
                }`}
              >
                <span className="font-mono text-xs text-foreground">{s.label}</span>
                <span className="truncate text-[11px] text-muted-foreground">{s.detail}</span>
              </button>
            ))}
          </div>
        )}
      </div>

      {signatureHint && (
        <div className="font-mono text-[11px] text-muted-foreground">
          {signatureHint.fn.params ? (
            <>
              {signatureHint.fn.name}(
              {signatureHint.fn.params.map((param, i) => (
                <span key={param}>
                  {i > 0 && ', '}
                  <span
                    className={
                      i === signatureHint.activeArg
                        ? 'rounded bg-sky-500/15 px-0.5 font-semibold text-sky-700 dark:text-sky-400'
                        : undefined
                    }
                  >
                    {param}
                  </span>
                </span>
              ))}
              )
            </>
          ) : (
            signatureHint.fn.signature
          )}
        </div>
      )}

      {status === 'invalid' && validation?.error && (
        <div className="text-[11px] text-red-600 dark:text-red-400">{validation.error}</div>
      )}
      {status === 'valid' && validation?.description && (
        <div className="text-[11px] text-muted-foreground italic">“{validation.description}”</div>
      )}
      {status === 'unknown' && (
        <div className="text-[11px] text-amber-600 dark:text-amber-400">
          Validation unavailable — is the API running with the /filters endpoints? The expression will be added unchecked.
        </div>
      )}
    </div>
  );
});
