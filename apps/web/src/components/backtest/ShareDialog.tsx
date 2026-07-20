import { useState } from 'react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '../ui/dialog';
import { Button } from '../ui/button';
import { Input } from '../ui/input';
import { Switch } from '../ui/switch';
import { backtestApi } from '../../api/backtestApi';
import { toast } from '../../hooks/use-toast';
import { Check, Copy, Link2, Loader2 } from 'lucide-react';

interface ShareDialogProps {
  backtestId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/**
 * Mints public share links for a completed backtest. Every "Create link" click makes a
 * fresh, independent link (each with its own 30-day clock) — there is no revocation, so
 * the copy below says so plainly.
 */
export function ShareDialog({ backtestId, open, onOpenChange }: ShareDialogProps) {
  const [includeConfig, setIncludeConfig] = useState(false);
  const [title, setTitle] = useState('');
  const [isCreating, setIsCreating] = useState(false);
  const [shareUrl, setShareUrl] = useState<string | null>(null);
  const [expiresAt, setExpiresAt] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);

  const handleCreate = async () => {
    setIsCreating(true);
    setCopied(false);
    try {
      const result = await backtestApi.createShare(backtestId, {
        includeConfig,
        title: title.trim() || undefined,
      });
      setShareUrl(`${window.location.origin}/share/${result.shareId}`);
      setExpiresAt(result.expiresAt);
    } catch (e) {
      console.error('Failed to create share link:', e);
      toast({
        title: 'Share failed',
        description: 'Could not create a share link. Please try again.',
        variant: 'destructive',
      });
    } finally {
      setIsCreating(false);
    }
  };

  const handleCopy = async () => {
    if (!shareUrl) return;
    try {
      await navigator.clipboard.writeText(shareUrl);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      toast({
        title: 'Copy failed',
        description: 'Select the link text and copy it manually.',
        variant: 'destructive',
      });
    }
  };

  const handleOpenChange = (next: boolean) => {
    onOpenChange(next);
    if (!next) {
      // Keep toggle/title so re-opening feels continuous, but clear the minted link —
      // each open starts a fresh mint decision.
      setShareUrl(null);
      setExpiresAt(null);
      setCopied(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Share backtest</DialogTitle>
          <DialogDescription>
            Creates a public, read-only snapshot anyone can view — no account needed.
            Links expire after 30 days and cannot be revoked early.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div>
            <label htmlFor="share-title" className="mb-1.5 block text-[11px] uppercase tracking-widest text-muted-foreground">
              Title (optional)
            </label>
            <Input
              id="share-title"
              value={title}
              maxLength={100}
              placeholder="e.g. Gap-up momentum, Q2"
              onChange={(e) => setTitle(e.target.value)}
            />
          </div>

          <div className="flex items-center justify-between gap-4 rounded-md border border-border/60 p-3">
            <div>
              <div className="text-sm font-medium">Include strategy configuration</div>
              <p className="text-xs text-muted-foreground">
                Off: viewers see results only — entry filters and exit settings stay hidden.
              </p>
            </div>
            <Switch checked={includeConfig} onCheckedChange={setIncludeConfig} />
          </div>

          {shareUrl ? (
            <div className="space-y-2">
              <div className="flex items-center gap-2">
                <Input readOnly value={shareUrl} className="font-mono text-xs" onFocus={(e) => e.target.select()} />
                <Button variant="outline" size="sm" onClick={handleCopy}>
                  {copied ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
                </Button>
              </div>
              <p className="text-xs text-muted-foreground">
                {expiresAt
                  ? `Expires ${new Date(expiresAt).toLocaleDateString()}.`
                  : 'Expires in 30 days.'}{' '}
                Creating another link mints a new URL; this one keeps working until it expires.
              </p>
              <Button variant="outline" size="sm" onClick={handleCreate} disabled={isCreating}>
                {isCreating ? (
                  <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />
                ) : (
                  <Link2 className="mr-1.5 h-4 w-4" />
                )}
                Create another link
              </Button>
            </div>
          ) : (
            <Button onClick={handleCreate} disabled={isCreating} className="w-full">
              {isCreating ? (
                <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />
              ) : (
                <Link2 className="mr-1.5 h-4 w-4" />
              )}
              Create link
            </Button>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
