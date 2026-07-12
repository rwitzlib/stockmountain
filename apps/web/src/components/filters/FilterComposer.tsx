import { useMemo, useRef, useState, forwardRef, useImperativeHandle, type Ref } from 'react';
import { Button } from '../ui/button';
import { Input } from '../ui/input';
import type { BuilderToken, FilterPaletteGroup } from '../../types/filters';

export interface FilterComposerRef {
  setExpression: (expression: string) => void;
}

interface FilterComposerProps {
  onAddFilter: (expression: string) => void;
  addButtonLabel?: string;
  allowCode?: boolean;
  initialMode?: 'builder' | 'code';
  disabled?: boolean;
  palette?: FilterPaletteGroup[];
}

interface EditableToken {
  type: 'editable';
  value: string;
  editing: boolean;
}

type ComposerToken = BuilderToken | EditableToken;

const DEFAULT_PALETTE: FilterPaletteGroup[] = [
  {
    label: 'Literals',
    buttons: [
      { label: 'close', token: { type: 'literal', value: 'close' } },
      { label: 'open', token: { type: 'literal', value: 'open' } },
      { label: 'high', token: { type: 'literal', value: 'high' } },
      { label: 'low', token: { type: 'literal', value: 'low' } },
      { label: 'vwap', token: { type: 'literal', value: 'vwap' } },
      { label: 'time', token: { type: 'literal', value: 'time' } },
    ],
  },
  {
    label: 'Indicators',
    buttons: [
      { label: 'sma()', token: { type: 'func', name: 'sma', args: ['14'] } },
      { label: 'ema()', token: { type: 'func', name: 'ema', args: ['14'] } },
      { label: 'macd()', token: { type: 'func', name: 'macd', args: ['12', '26', '9', 'ema'] } },
      { label: 'adv()', token: { type: 'func', name: 'adv', args: [] } },
    ],
  },
  {
    label: 'Operators',
    buttons: [
      { label: '>', token: { type: 'op', value: '>' } },
      { label: '<', token: { type: 'op', value: '<' } },
      { label: '>=', token: { type: 'op', value: '>=' } },
      { label: '<=', token: { type: 'op', value: '<=' } },
      { label: '=', token: { type: 'op', value: '=' } },
      { label: '!=', token: { type: 'op', value: '!=' } },
    ],
  },
  {
    label: 'Logical',
    buttons: [
      { label: 'AND', token: { type: 'logic', value: 'AND' } },
      { label: 'OR', token: { type: 'logic', value: 'OR' } },
    ],
  },
  {
    label: 'Range',
    buttons: [
      { label: '[1m]', token: { type: 'range', timeframe: '1m' } },
      { label: '[5m]', token: { type: 'range', timeframe: '5m' } },
      { label: '[1d]', token: { type: 'range', timeframe: '1d' } },
      { label: '[, 5]', token: { type: 'range', candles: '5' } },
    ],
  },
];

const tokenLabel = (token: ComposerToken) => {
  if (token.type === 'literal') return token.value;
  if (token.type === 'op' || token.type === 'logic') return token.value;
  if (token.type === 'func') return `${token.name}(${token.args.join(',')})`;
  if (token.type === 'range') {
    if (token.timeframe && token.candles) return `[${token.timeframe}, ${token.candles}]`;
    if (token.timeframe) return `[${token.timeframe}]`;
    if (token.candles) return `[, ${token.candles}]`;
    return '[]';
  }
  if (token.type === 'editable') return token.value;
  return '';
};

const serializeTokens = (tokens: ComposerToken[]) =>
  tokens
    .map(token => {
      switch (token.type) {
        case 'literal':
          return token.value;
        case 'op':
        case 'logic':
          return token.value;
        case 'func':
          return `${token.name}(${token.args.join(',')})`;
        case 'range':
          return tokenLabel(token);
        case 'editable':
          return token.value;
        default:
          return '';
      }
    })
    .filter(Boolean)
    .join(' ')
    .trim();

export const FilterComposer = forwardRef(function FilterComposer({
  onAddFilter,
  addButtonLabel = 'Add Filter',
  allowCode = true,
  initialMode = 'builder',
  disabled = false,
  palette,
}: FilterComposerProps, ref: Ref<FilterComposerRef>) {
  const [mode, setMode] = useState<'builder' | 'code'>(initialMode);
  const [tokens, setTokens] = useState<ComposerToken[]>([]);
  const [codeInput, setCodeInput] = useState('');
  const editableRef = useRef<HTMLInputElement | null>(null);
  const shouldFocusEditable = useRef(false);

  useImperativeHandle(ref, () => ({
    setExpression: (expression: string) => {
      setMode('code');
      setCodeInput(expression);
    },
  }));

  const paletteGroups = useMemo(() => palette ?? DEFAULT_PALETTE, [palette]);

  const expression = useMemo(() => serializeTokens(tokens), [tokens]);

  const builderDisabled = disabled || !expression;
  const codeDisabled = disabled || !codeInput.trim();

  const handleAddToken = (token: BuilderToken) => {
    if (disabled) return;
    setTokens(prev => [...prev, token]);
  };

  const handleAddEditable = () => {
    if (disabled) return;
    shouldFocusEditable.current = true;
    setTokens(prev => [...prev, { type: 'editable', value: '', editing: true }]);
  };

  const handleRemoveToken = (index: number) => {
    if (disabled) return;
    setTokens(prev => prev.filter((_, idx) => idx !== index));
  };

  const handleTokenClick = (index: number) => {
    if (disabled) return;
    const token = tokens[index];
    if (token?.type === 'literal') {
      shouldFocusEditable.current = true;
      setTokens(prev => prev.map((t, idx) => (idx === index ? { type: 'editable', value: token.value, editing: true } : t)));
    }
  };

  const handleEditableChange = (index: number, value: string) => {
    setTokens(prev => prev.map((token, idx) => (idx === index && token.type === 'editable' ? { ...token, value } : token)));
  };

  const finalizeEditable = (index: number) => {
    setTokens(prev =>
      prev.flatMap((token, idx) => {
        if (idx !== index || token.type !== 'editable') return token;
        const trimmed = token.value.trim();
        if (!trimmed) return [];
        return { type: 'literal', value: trimmed } as BuilderToken;
      }),
    );
  };

  const handleAddFilter = (expr: string) => {
    const trimmed = expr.trim();
    if (!trimmed) return;
    onAddFilter(trimmed);
    setTokens([]);
    setCodeInput('');
  };

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between mb-2 border-b border-border pb-2">
        <h3 className="text-xs font-mono uppercase tracking-wider text-primary dark:text-cyan-400"># Filters</h3>
        <div className="flex items-center gap-1">
          <Button
            variant="outline"
            size="sm"
            onClick={() => setMode('builder')}
            className={`font-mono text-xs uppercase px-3 py-1 transition-all ${
              mode === 'builder'
                ? 'bg-primary/10 dark:bg-cyan-950 text-primary dark:text-cyan-400 border-primary dark:border-cyan-700'
                : 'bg-background dark:bg-gray-900 text-muted-foreground border-border dark:border-gray-700 hover:text-primary dark:hover:text-cyan-400 hover:border-primary dark:hover:border-cyan-700'
            }`}
            disabled={disabled}
          >
            Builder
          </Button>
          {allowCode && (
            <Button
              variant="outline"
              size="sm"
              onClick={() => setMode('code')}
              className={`font-mono text-xs uppercase px-3 py-1 transition-all ${
                mode === 'code'
                  ? 'bg-primary/10 dark:bg-cyan-950 text-primary dark:text-cyan-400 border-primary dark:border-cyan-700'
                  : 'bg-background dark:bg-gray-900 text-muted-foreground border-border dark:border-gray-700 hover:text-primary dark:hover:text-cyan-400 hover:border-primary dark:hover:border-cyan-700'
              }`}
              disabled={disabled}
            >
              Code
            </Button>
          )}
        </div>
      </div>

      {mode === 'builder' ? (
        <div className="space-y-3">
          {paletteGroups.map(group => (
            <div key={group.label}>
              <div className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground mb-2">:: {group.label}</div>
              <div className="flex flex-wrap gap-2">
                {group.buttons.map(button => (
                  <Button
                    key={button.label}
                    variant="outline"
                    size="sm"
                    onClick={() => handleAddToken(button.token)}
                    disabled={disabled}
                    className={
                      button.className ??
                      'bg-background dark:bg-gray-900 border-border dark:border-gray-700 text-primary dark:text-cyan-400 hover:border-primary dark:hover:border-cyan-700 hover:bg-primary/10 dark:hover:bg-cyan-950/30 font-mono text-xs px-2 py-1 transition-all disabled:opacity-50'
                    }
                  >
                    {button.label}
                  </Button>
                ))}
                {group.label === 'Literals' && (
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={handleAddEditable}
                    disabled={disabled}
                    className="bg-background dark:bg-gray-900 border-border dark:border-gray-700 text-muted-foreground dark:text-slate-300 hover:border-primary dark:hover:border-cyan-700 hover:text-primary dark:hover:text-cyan-400 font-mono text-xs px-2 py-1 transition-all disabled:opacity-50"
                  >
                    Custom...
                  </Button>
                )}
              </div>
            </div>
          ))}

          <div className="flex flex-wrap gap-2 min-h-[38px] p-2 rounded border border-border bg-muted/30 dark:bg-gray-950/40">
            {tokens.length === 0 ? (
              <span className="text-xs text-muted-foreground italic font-mono">{'>> '}Build expression by clicking palette buttons...</span>
            ) : (
              tokens.map((token, index) => (
                <div
                  key={`${tokenLabel(token)}-${index}`}
                  className="flex items-center gap-1 px-3 py-1.5 border rounded-lg transition-all duration-200 ease-in-out bg-card dark:bg-gray-900/70 border-border dark:border-gray-700 text-primary dark:text-cyan-300 font-mono text-xs"
                >
                  {token.type === 'editable' ? (
                    <Input
                      ref={editableRef}
                      type="text"
                      className="w-28 h-6 text-xs"
                      value={token.value}
                      onChange={event => handleEditableChange(index, event.target.value)}
                      onBlur={() => finalizeEditable(index)}
                      onKeyDown={event => {
                        if (event.key === 'Enter') {
                          event.preventDefault();
                          finalizeEditable(index);
                        } else if (event.key === 'Escape') {
                          event.preventDefault();
                          handleRemoveToken(index);
                        }
                      }}
                      disabled={disabled}
                    />
                  ) : (
                    <button
                      onClick={() => handleTokenClick(index)}
                      className="hover:underline decoration-dotted underline-offset-2"
                      disabled={disabled}
                    >
                      {tokenLabel(token)}
                    </button>
                  )}
                  <button
                    className="text-sm font-bold opacity-60 hover:opacity-100 hover:scale-125 transition-all text-foreground"
                    onClick={() => handleRemoveToken(index)}
                    disabled={disabled}
                  >
                    ×
                  </button>
                </div>
              ))
            )}
          </div>

          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              onClick={() => setTokens([])}
              disabled={tokens.length === 0 || disabled}
              className="bg-background dark:bg-gray-900 border-border dark:border-gray-700 text-muted-foreground hover:border-red-500 dark:hover:border-red-700 hover:text-red-600 dark:hover:text-red-400 font-mono text-xs uppercase px-3 py-1 transition-all disabled:opacity-50"
            >
              Clear
            </Button>
            <Button
              onClick={() => handleAddFilter(expression)}
              disabled={builderDisabled}
              className="bg-green-100 dark:bg-green-950 border-green-300 dark:border-green-700 text-green-700 dark:text-green-400 hover:bg-green-200 dark:hover:bg-green-900 hover:border-green-400 dark:hover:border-green-500 font-mono text-xs uppercase px-3 py-1 transition-all disabled:opacity-50"
            >
              {addButtonLabel}
            </Button>
          </div>
        </div>
      ) : (
        <div className="flex items-center gap-2">
          <Input
            value={codeInput}
            onChange={event => setCodeInput(event.target.value)}
            onKeyDown={event => {
              if (event.key === 'Enter' && !codeDisabled) {
                event.preventDefault();
                handleAddFilter(codeInput);
              }
            }}
            placeholder="e.g. adv() > 2000000 [1d]"
            disabled={disabled}
            className="flex-1 bg-background dark:bg-gray-950 border-border dark:border-gray-700 text-foreground dark:text-cyan-400 font-mono text-xs px-3 py-2 rounded placeholder:text-muted-foreground hover:border-primary dark:hover:border-cyan-700 focus:border-primary dark:focus:border-cyan-500 transition-colors"
          />
          <Button
            onClick={() => handleAddFilter(codeInput)}
            disabled={codeDisabled}
            className="bg-green-100 dark:bg-green-950 border-green-300 dark:border-green-700 text-green-700 dark:text-green-400 hover:bg-green-200 dark:hover:bg-green-900 hover:border-green-400 dark:hover:border-green-500 font-mono text-xs uppercase px-3 py-1 transition-all disabled:opacity-50"
          >
            {addButtonLabel}
          </Button>
        </div>
      )}
    </div>
  );
});

