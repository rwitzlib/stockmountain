import { X } from 'lucide-react';

interface StudiesModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export function StudiesModal({ isOpen, onClose }: StudiesModalProps) {
  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-card rounded-xl border border-border/80 shadow-lg w-full max-w-sm">
        <div className="flex items-center justify-between p-4 border-b border-border">
          <h2 className="text-lg font-semibold tracking-tight text-foreground">Studies</h2>
          <button
            onClick={onClose}
            className="text-muted-foreground hover:text-foreground"
          >
            <X className="w-5 h-5" />
          </button>
        </div>
        
        <div className="p-4 space-y-3">
          <button
            onClick={() => {
              console.log('Edit studies clicked');
              // Add your edit studies logic here
            }}
            className="w-full px-4 py-3 text-left bg-card hover:bg-accent border border-border rounded-lg text-foreground transition-colors"
          >
            Edit Studies
          </button>
          
          <button
            onClick={() => {
              console.log('Load study set clicked');
              // Add your load study set logic here
            }}
            className="w-full px-4 py-3 text-left bg-card hover:bg-accent border border-border rounded-lg text-foreground transition-colors"
          >
            Load Study Set
          </button>
        </div>
      </div>
    </div>
  );
} 