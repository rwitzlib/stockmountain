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
        <h3 className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground">Filters</h3>
        <div className="flex items-center gap-1">
          <Button
            variant="outline"
            size="sm"
            onClick={() => setMode('builder')}
            className={`text-xs px-3 py-1 transition-colors border-border ${
              mode === 'builder'
                ? 'bg-accent text-foreground font-medium'
                : 'bg-transparent text-muted-foreground hover:bg-accent hover:text-foreground'
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
              className={`text-xs px-3 py-1 transition-colors border-border ${
                mode === 'code'
                  ? 'bg-accent text-foreground font-medium'
                  : 'bg-transparent text-muted-foreground hover:bg-accent hover:text-foreground'
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
              <div className="text-[11px] font-medium uppercase tracking-widest text-muted-foreground mb-2">{group.label}</div>
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
                      'rounded-md border-border/60 bg-muted/50 text-foreground hover:bg-accent hover:text-foreground font-mono text-xs px-2 py-1 transition-colors disabled:opacity-50'
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
                    className="rounded-md border-border/60 bg-muted/50 text-muted-foreground hover:bg-accent hover:text-foreground text-xs px-2 py-1 transition-colors disabled:opacity-50"
                  >
                    Custom...
                  </Button>
                )}
              </div>
            </div>
          ))}

          <div className="flex flex-wrap gap-2 min-h-[38px] p-2 rounded-lg border border-border bg-muted/30">
            {tokens.length === 0 ? (
              <span className="text-xs text-muted-foreground italic">Build an expression by clicking palette buttons...</span>
            ) : (
              tokens.map((token, index) => (
                <div
                  key={`${tokenLabel(token)}-${index}`}
                  className="flex items-center gap-1 rounded-md border border-border/60 bg-muted/50 px-2.5 py-1.5 font-mono text-xs text-foreground transition-colors"
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
                    className="text-sm font-bold opacity-60 hover:opacity-100 transition-opacity text-foreground"
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
              className="border-border bg-transparent text-muted-foreground hover:bg-accent hover:text-red-600 dark:hover:text-red-400 text-xs px-3 py-1 transition-colors disabled:opacity-50"
            >
              Clear
            </Button>
            <Button
              onClick={() => handleAddFilter(expression)}
              disabled={builderDisabled}
              className="bg-primary text-primary-foreground hover:bg-primary/90 text-xs font-medium px-3 py-1 transition-colors disabled:opacity-50"
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
            className="flex-1 rounded-lg border border-input bg-card text-foreground font-mono text-xs px-3 py-2 placeholder:text-muted-foreground transition-colors"
          />
          <Button
            onClick={() => handleAddFilter(codeInput)}
            disabled={codeDisabled}
            className="bg-primary text-primary-foreground hover:bg-primary/90 text-xs font-medium px-3 py-1 transition-colors disabled:opacity-50"
          >
            {addButtonLabel}
          </Button>
        </div>
      )}
    </div>
  );
});

