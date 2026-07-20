import { useState, useEffect } from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '../ui/dialog';
import { Button } from '../ui/button';
import { StrategyForm } from '../forms/StrategyForm';
import { Strategy } from '../../types/strategy';

interface StrategyModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (strategy: Strategy) => void;
  isLoading?: boolean;
  initialData?: Strategy;
}

export function StrategyModal({ isOpen, onClose, onSubmit, isLoading, initialData }: StrategyModalProps) {
  const [formData, setFormData] = useState<Strategy | undefined>(initialData);

  // Initialize form data with initialData when provided (for cloning)
  useEffect(() => {
    if (initialData) {
      setFormData(initialData);
    }
  }, [initialData]);

  const handleSubmit = (strategy: Strategy) => {
    onSubmit(strategy);
  };

  const handleClose = () => {
    setFormData(undefined);
    onClose();
  };

  return (
    <Dialog open={isOpen} onOpenChange={handleClose}>
      <DialogContent className="sm:max-w-[900px] max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="text-xl font-semibold tracking-tight text-foreground">
            {initialData ? 'Clone Strategy' : 'Create New Strategy'}
          </DialogTitle>
        </DialogHeader>
        
        <div className="space-y-6">
          <StrategyForm
            initialData={formData}
            onSubmit={handleSubmit}
            isLoading={isLoading}
          />
        </div>
      </DialogContent>
    </Dialog>
  );
}