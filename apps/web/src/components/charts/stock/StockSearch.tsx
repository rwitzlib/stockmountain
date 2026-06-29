import { useState } from 'react';
import { Search } from 'lucide-react';

interface StockSearchProps {
  value: string;
  onSubmit: (symbol: string) => void;
}

export function StockSearch({ value, onSubmit }: StockSearchProps) {
  const [input, setInput] = useState(value);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit(input.toUpperCase());
  };

  return (
    <form onSubmit={handleSubmit} className="flex-1">
      <div className="relative">
        <input
          type="text"
          value={input}
          onChange={e => setInput(e.target.value)}
          placeholder="TICKER"
          className="w-full max-w-[120px] pl-8 pr-3 py-1 border border-border bg-background text-foreground dark:text-cyan-400 text-xs font-mono uppercase tracking-wider hover:border-primary dark:hover:border-cyan-700 focus:outline-none focus:border-primary dark:focus:border-cyan-500 transition-colors placeholder:text-muted-foreground"
        />
        <Search className="absolute left-2 top-1/2 -translate-y-1/2 w-3 h-3 text-muted-foreground" />
      </div>
    </form>
  );
}