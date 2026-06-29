import { Button } from '../ui/button';
import { Switch } from '../ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../ui/table';
import type { FilterItem } from '../../types/filters';

interface FilterListProps {
  filters: FilterItem[];
  onToggle: (id: string) => void;
  onRemove: (id: string) => void;
  onEdit?: (id: string) => void;
  emptyLabel?: string;
  className?: string;
  disabled?: boolean;
}

export function FilterList({
  filters,
  onToggle,
  onRemove,
  onEdit,
  emptyLabel = '[ No filters configured ]',
  className,
  disabled = false,
}: FilterListProps) {
  if (!filters.length) {
    return (
      <div className={`text-xs text-muted-foreground font-mono ${className ?? ''}`.trim()}>
        {emptyLabel}
      </div>
    );
  }

  return (
    <Table className={className}>
      <TableHeader>
        <TableRow className="border-border hover:bg-muted/50">
          <TableHead className="text-[10px] font-mono uppercase text-muted-foreground">Enabled</TableHead>
          <TableHead className="text-[10px] font-mono uppercase text-muted-foreground">Expression</TableHead>
          <TableHead className="w-32 text-[10px] font-mono uppercase text-muted-foreground">Actions</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {filters.map(filter => (
          <TableRow key={filter.id} className="border-border hover:bg-muted/50">
            <TableCell>
              <Switch
                checked={filter.enabled}
                onCheckedChange={() => onToggle(filter.id)}
                disabled={disabled}
              />
            </TableCell>
            <TableCell className="font-mono text-xs text-primary dark:text-cyan-400 whitespace-pre-wrap break-words">
              {filter.expression}
            </TableCell>
            <TableCell>
              <div className="flex gap-2">
                {onEdit && (
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => onEdit(filter.id)}
                    disabled={disabled}
                    className="bg-background dark:bg-gray-900 border-border dark:border-gray-700 text-primary dark:text-cyan-400 hover:border-primary dark:hover:border-cyan-700 hover:bg-primary/10 dark:hover:bg-cyan-950/30 font-mono text-xs uppercase px-2 py-1 transition-all disabled:opacity-50"
                  >
                    Edit
                  </Button>
                )}
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => onRemove(filter.id)}
                  disabled={disabled}
                  className="bg-background dark:bg-gray-900 border-border dark:border-gray-700 text-red-600 dark:text-red-400 hover:border-red-500 dark:hover:border-red-700 hover:bg-red-100/50 dark:hover:bg-red-950/30 font-mono text-xs uppercase px-2 py-1 transition-all disabled:opacity-50"
                >
                  Remove
                </Button>
              </div>
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}


