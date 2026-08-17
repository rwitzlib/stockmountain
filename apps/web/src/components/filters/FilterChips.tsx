import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { filtersApi } from '../../api/filtersApi';
import { formatTimeframe, isLogical, serializeAst } from './filterExpression';
import type { FilterAstNode } from '../../types/filters';

interface FilterChipsProps {
  expression: string;
  /** When provided, segment clicks let the user edit that piece; edits re-serialize the AST. */
  onChange?: (expression: string) => void;
  className?: string;
}

type Role = 'function' | 'data' | 'literal' | 'op' | 'logic' | 'timeframe' | 'paren';

interface Segment {
  text: string;
  role: Role;
  /** Path from the root to the edited node, used to apply edits immutably. */
  path: number[];
  edit?: 'value' | 'op' | 'timeframe';
}

const ROLE_CLASS: Record<Role, string> = {
  function: 'text-sky-700 dark:text-sky-400',
  data: 'text-foreground',
  literal: 'text-amber-700 dark:text-amber-400',
  op: 'text-muted-foreground font-semibold',
  logic: 'text-purple-700 dark:text-purple-400 font-semibold',
  timeframe: 'text-green-700 dark:text-green-400',
  paren: 'text-muted-foreground',
};

const OPERATORS = ['>', '<', '>=', '<=', '=', '!='];
const TIMEFRAMES = ['1m', '5m', '15m', '30m', '1h', '1d', '1w'];

/**
 * Flattens the presentation AST into ordered display segments. Operands (functions,
 * field access) render as single segments; only leaf-editable pieces get an edit kind.
 */
function flatten(node: FilterAstNode, path: number[], out: Segment[]) {
  switch (node.kind) {
    case 'range':
      if (node.inner) flatten(node.inner, [...path, 0], out);
      out.push({
        text: node.timeframe
          ? `[${formatTimeframe(node.timeframe)}${node.candles ? `, ${node.candles}` : ''}]`
          : `[, ${node.candles ?? ''}]`,
        role: 'timeframe',
        path,
        edit: 'timeframe',
      });
      return;
    case 'binary': {
      const logical = isLogical(node);
      // Mirror serializeOperand(): a differently-grouped logical child keeps its parentheses.
      const grouped = (child?: FilterAstNode) => logical && isLogical(child) && child!.op !== node.op;
      if (node.left) {
        if (grouped(node.left)) out.push({ text: '(', role: 'paren', path });
        flatten(node.left, [...path, 0], out);
        if (grouped(node.left)) out.push({ text: ')', role: 'paren', path });
      }
      out.push({
        text: node.op ?? '',
        role: logical ? 'logic' : 'op',
        path,
        edit: logical ? undefined : 'op',
      });
      if (node.right) {
        if (grouped(node.right)) out.push({ text: '(', role: 'paren', path });
        flatten(node.right, [...path, 1], out);
        if (grouped(node.right)) out.push({ text: ')', role: 'paren', path });
      }
      return;
    }
    case 'unary': {
      out.push({ text: node.op ?? 'NOT', role: 'logic', path });
      const grouped = isLogical(node.inner);
      if (grouped) out.push({ text: '(', role: 'paren', path });
      if (node.inner) flatten(node.inner, [...path, 0], out);
      if (grouped) out.push({ text: ')', role: 'paren', path });
      return;
    }
    case 'literal':
      out.push({ text: node.value ?? '', role: 'literal', path, edit: 'value' });
      return;
    case 'data':
      out.push({ text: node.field ?? '', role: 'data', path });
      return;
    case 'function':
    case 'field':
    default:
      out.push({ text: serializeAst(node), role: node.kind === 'function' || node.kind === 'field' ? 'function' : 'data', path });
  }
}

/** Child accessor consistent with the paths produced by flatten(). */
const childOf = (node: FilterAstNode, index: number): FilterAstNode | undefined => {
  if (node.kind === 'range' || node.kind === 'unary') return node.inner;
  if (node.kind === 'binary') return index === 0 ? node.left : node.right;
  return undefined;
};

const withChild = (node: FilterAstNode, index: number, child: FilterAstNode): FilterAstNode => {
  if (node.kind === 'range' || node.kind === 'unary') return { ...node, inner: child };
  if (node.kind === 'binary') return index === 0 ? { ...node, left: child } : { ...node, right: child };
  return node;
};

function updateAt(root: FilterAstNode, path: number[], update: (node: FilterAstNode) => FilterAstNode): FilterAstNode {
  if (path.length === 0) return update(root);
  const [head, ...rest] = path;
  const child = childOf(root, head);
  if (!child) return root;
  return withChild(root, head, updateAt(child, rest, update));
}

function parseTimeframeToken(token: string): { multiplier: number; timespan: string } | null {
  const match = token.match(/^(\d+)(mo|[mhdwqy])$/);
  if (!match) return null;
  const timespan =
    { m: 'minute', h: 'hour', d: 'day', w: 'week', mo: 'month', q: 'quarter', y: 'year' }[match[2]];
  return timespan ? { multiplier: Number(match[1]), timespan } : null;
}

export function FilterChips({ expression, onChange, className }: FilterChipsProps) {
  const [editingPath, setEditingPath] = useState<string | null>(null);
  const [draft, setDraft] = useState('');

  const { data: results } = useQuery({
    queryKey: ['filterAst', expression],
    queryFn: () => filtersApi.validate([expression]),
    staleTime: Infinity,
    retry: 1,
  });

  const ast = results?.[0]?.valid ? results[0].ast : undefined;

  const segments = useMemo(() => {
    if (!ast) return null;
    const out: Segment[] = [];
    flatten(ast, [], out);
    return out;
  }, [ast]);

  // Unparseable (legacy) or still loading — fall back to plain text
  if (!segments) {
    return (
      <code className={className ?? 'rounded-md border border-border/60 bg-muted/50 px-2.5 py-1.5 font-mono text-xs'}>
        {expression}
      </code>
    );
  }

  const commitEdit = (segment: Segment, raw: string) => {
    setEditingPath(null);
    const value = raw.trim();
    if (!value || !ast || !onChange) return;

    let nextAst: FilterAstNode | null = null;
    if (segment.edit === 'value') {
      nextAst = updateAt(ast, segment.path, (n) => ({ ...n, value }));
    } else if (segment.edit === 'op') {
      if (!OPERATORS.includes(value)) return;
      nextAst = updateAt(ast, segment.path, (n) => ({ ...n, op: value }));
    } else if (segment.edit === 'timeframe') {
      const parsed = parseTimeframeToken(value);
      if (!parsed) return;
      nextAst = updateAt(ast, segment.path, (n) => ({ ...n, timeframe: parsed }));
    }
    if (nextAst) {
      const next = serializeAst(nextAst);
      if (next !== expression) onChange(next);
    }
  };

  return (
    <span className={`inline-flex flex-wrap items-center gap-1 ${className ?? ''}`}>
      {segments.map((segment, i) => {
        const key = `${segment.path.join('.')}-${i}`;
        const editable = onChange && segment.edit;
        if (editingPath === key && editable) {
          if (segment.edit === 'op' || segment.edit === 'timeframe') {
            const options = segment.edit === 'op' ? OPERATORS : TIMEFRAMES;
            return (
              <select
                key={key}
                autoFocus
                defaultValue={segment.edit === 'op' ? segment.text : segment.text.replace(/[[\]\s]/g, '').split(',')[0]}
                onChange={(e) => commitEdit(segment, e.target.value)}
                onBlur={() => setEditingPath(null)}
                className="h-6 rounded border border-input bg-card px-1 font-mono text-xs"
              >
                {options.map((o) => (
                  <option key={o} value={o}>{o}</option>
                ))}
              </select>
            );
          }
          return (
            <input
              key={key}
              autoFocus
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              onBlur={() => commitEdit(segment, draft)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') commitEdit(segment, draft);
                if (e.key === 'Escape') setEditingPath(null);
              }}
              className="h-6 w-16 rounded border border-input bg-card px-1 font-mono text-xs"
            />
          );
        }
        return (
          <button
            key={key}
            type="button"
            disabled={!editable}
            onClick={() => {
              if (!editable) return;
              setDraft(segment.text);
              setEditingPath(key);
            }}
            className={`rounded px-1 py-0.5 font-mono text-xs ${ROLE_CLASS[segment.role]} ${
              editable ? 'cursor-pointer hover:bg-accent hover:underline decoration-dotted underline-offset-2' : 'cursor-default'
            }`}
          >
            {segment.text}
          </button>
        );
      })}
    </span>
  );
}
