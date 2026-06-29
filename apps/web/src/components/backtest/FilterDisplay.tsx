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
        <div className="inline-flex items-center gap-1 px-2 py-1 bg-blue-50 text-blue-700 rounded text-sm">
          <span className="font-medium">{operand.name?.toUpperCase()}</span>
          {operand.parameters && (
            <span className="text-xs">({operand.parameters})</span>
          )}
          {operand.modifier && (
            <span className="text-xs">.{operand.modifier}</span>
          )}
          {operand.timeframe && (
            <span className="text-xs">[{formatTimeframe(operand.timeframe)}]</span>
          )}
        </div>
      );
    
    case 'PriceAction':
      return (
        <div className="inline-flex items-center gap-1 px-2 py-1 bg-green-50 text-green-700 rounded text-sm">
          <span className="font-medium">{operand.name}</span>
          {operand.modifier && (
            <span className="text-xs">.{operand.modifier}</span>
          )}
          {operand.timeframe && (
            <span className="text-xs">[{formatTimeframe(operand.timeframe)}]</span>
          )}
        </div>
      );
    
    case 'Fixed':
      return (
        <div className="inline-flex items-center px-2 py-1 bg-gray-50 text-gray-700 rounded text-sm">
          <span className="font-medium">{operand.value}</span>
        </div>
      );
    
    default:
      return (
        <div className="inline-flex items-center px-2 py-1 bg-gray-50 text-gray-700 rounded text-sm">
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
    <div className="p-3 bg-gray-50 rounded-md border border-gray-200">
      <div className="flex flex-wrap items-center gap-2 mb-2">
        <OperandDisplay operand={filter.firstOperand} />
        <span className="px-2 py-1 bg-purple-100 text-purple-700 rounded text-sm font-medium">
          {getOperatorSymbol(filter.operator)}
        </span>
        <OperandDisplay operand={filter.secondOperand} />
      </div>
      
      <div className="flex justify-between items-center text-xs text-gray-500">
        <div className="flex items-center gap-4">
          <span>Collection: <span className="font-medium">{filter.collectionModifier}</span></span>
          {filter.timeframe && (
            <span>Candles: <span className="font-medium">{filter.timeframe.multiplier}</span></span>
          )}
        </div>
      </div>
    </div>
  );
}

export function FilterDisplay({ argument }: FilterDisplayProps) {
  if (!argument || !argument.filters || argument.filters.length === 0) {
    return (
      <div className="text-gray-500 text-sm">No filters configured</div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-2">
        <span className="text-sm text-gray-600">Operator:</span>
        <span className={`px-2 py-1 rounded text-xs font-medium ${
          argument.operator === 'AND' 
            ? 'bg-red-100 text-red-700' 
            : 'bg-yellow-100 text-yellow-700'
        }`}>
          {argument.operator}
        </span>
        <span className="text-xs text-gray-500">
          ({argument.filters.length} filter{argument.filters.length !== 1 ? 's' : ''})
        </span>
      </div>
      
      <div className="space-y-3">
        {argument.filters.map((filter, index) => (
          <div key={index} className="relative">
            {index > 0 && (
              <div className="flex justify-center mb-2">
                <span className={`px-2 py-1 rounded text-xs font-medium ${
                  argument.operator === 'AND' 
                    ? 'bg-red-100 text-red-700' 
                    : 'bg-yellow-100 text-yellow-700'
                }`}>
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