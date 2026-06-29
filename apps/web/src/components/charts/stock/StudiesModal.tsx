import { X } from 'lucide-react';

interface StudiesModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export function StudiesModal({ isOpen, onClose }: StudiesModalProps) {
  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white rounded-lg shadow-lg w-full max-w-sm">
        <div className="flex items-center justify-between p-4 border-b">
          <h2 className="text-lg font-semibold">Studies</h2>
          <button
            onClick={onClose}
            className="text-gray-500 hover:text-gray-700"
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
            className="w-full px-4 py-3 text-left bg-white hover:bg-gray-50 border rounded-md"
          >
            Edit Studies
          </button>
          
          <button
            onClick={() => {
              console.log('Load study set clicked');
              // Add your load study set logic here
            }}
            className="w-full px-4 py-3 text-left bg-white hover:bg-gray-50 border rounded-md"
          >
            Load Study Set
          </button>
        </div>
      </div>
    </div>
  );
} 