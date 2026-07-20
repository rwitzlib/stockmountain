import { Filter, ScanArgument, Operand } from '../../types/strategy';

interface FilterDisplayProps {
  argument: ScanArgument;
}

interface OperandDisplayProps {
  operand: Operand;
}

function OperandDisplay({ operand }: OperandDisplayProps) {
  const formatTimeframe = (timeframe: any) => {
    if (!timeframe) return '';
    return `${timeframe.multiplier}${timeframe.timespan[0]}`;
  };

  switch (operand.type) {
    case 'Study':
      return (
        <div className="inline-flex items-center gap-1 rounded-md border border-border/60 bg-muted/50 px-2 py-1 font-mono text-xs text-foreground">
          <span className="font-medium">{operand.name?.toUpperCase()}</span>
          {operand.parameters && (
            <span className="text-muted-foreground">({operand.parameters})</span>
          )}
          {operand.modifier && (
            <span className="text-muted-foreground">.{operand.modifier}</span>
          )}
          {operand.timeframe && (
            <span className="text-muted-foreground">[{formatTimeframe(operand.timeframe)}]</span>
          )}
        </div>
      );
    
    case 'PriceAction':
      return (
        <div className="inline-flex items-center gap-1 rounded-md border border-border/60 bg-muted/50 px-2 py-1 font-mono text-xs text-foreground">
          <span className="font-medium">{operand.name}</span>
          {operand.modifier && (
            <span className="text-muted-foreground">.{operand.modifier}</span>
          )}
          {operand.timeframe && (
            <span className="text-muted-foreground">[{formatTimeframe(operand.timeframe)}]</span>
          )}
        </div>
      );
    
    case 'Fixed':
      return (
        <div className="inline-flex items-center rounded-md border border-border/60 bg-muted/50 px-2 py-1 font-mono text-xs text-foreground">
          <span className="font-medium">{operand.value}</span>
        </div>
      );
    
    default:
      return (
        <div className="inline-flex items-center rounded-md border border-border/60 bg-muted/50 px-2 py-1 font-mono text-xs text-foreground">
          <span>{JSON.stringify(operand)}</span>
        </div>
      );
  }
}

function FilterItemDisplay({ filter }: { filter: Filter }) {
  const getOperatorSymbol = (operator: string) => {
    switch (operator) {
      case 'gt': return '>';
      case 'lt': return '<';
      case 'eq': return '=';
      case 'gte': return '≥';
      case 'lte': return '≤';
      case 'ne': return '≠';
      default: return operator;
    }
  };

  const formatTimeframe = (timeframe: any) => {
    if (!timeframe) return '';
    return `${timeframe.multiplier} ${timeframe.timespan}${timeframe.multiplier > 1 ? 's' : ''}`;
  };

  return (
    <div className="p-3 rounded-lg border border-border/60 bg-muted/30">
      <div className="flex flex-wrap items-center gap-2 mb-2">
        <OperandDisplay operand={filter.firstOperand} />
        <span className="px-2 py-1 rounded-md bg-muted font-mono text-xs font-medium text-foreground">
          {getOperatorSymbol(filter.operator)}
        </span>
        <OperandDisplay operand={filter.secondOperand} />
      </div>
      
      <div className="flex justify-between items-center text-xs text-muted-foreground">
        <div className="flex items-center gap-4">
          <span>Collection: <span className="font-medium text-foreground">{filter.collectionModifier}</span></span>
          {filter.timeframe && (
            <span>Candles: <span className="font-medium text-foreground">{filter.timeframe.multiplier}</span></span>
          )}
        </div>
      </div>
    </div>
  );
}

export function FilterDisplay({ argument }: FilterDisplayProps) {
  if (!argument || !argument.filters || argument.filters.length === 0) {
    return (
      <div className="text-muted-foreground text-sm">No filters configured</div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-2">
        <span className="text-sm text-muted-foreground">Operator:</span>
        <span className="rounded-full border border-border px-2 py-0.5 text-[10px] font-medium uppercase tracking-wider text-muted-foreground">
          {argument.operator}
        </span>
        <span className="text-xs text-muted-foreground">
          ({argument.filters.length} filter{argument.filters.length !== 1 ? 's' : ''})
        </span>
      </div>
      
      <div className="space-y-3">
        {argument.filters.map((filter, index) => (
          <div key={index} className="relative">
            {index > 0 && (
              <div className="flex justify-center mb-2">
                <span className="rounded-full border border-border px-2 py-0.5 text-[10px] font-medium uppercase tracking-wider text-muted-foreground">
                  {argument.operator}
                </span>
              </div>
            )}
            <FilterItemDisplay filter={filter} />
          </div>
        ))}
      </div>
    </div>
  );
} 