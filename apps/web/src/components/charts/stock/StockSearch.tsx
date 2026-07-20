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
          className="w-full max-w-[120px] pl-8 pr-3 py-1 rounded-lg border border-input bg-card text-foreground text-xs font-mono uppercase focus:outline-none focus:border-ring transition-colors placeholder:text-muted-foreground"
        />
        <Search className="absolute left-2 top-1/2 -translate-y-1/2 w-3 h-3 text-muted-foreground" />
      </div>
    </form>
  );
}