import { useCallback, useMemo, useRef, useState, type ReactNode } from 'react';
import * as RadixToast from '@radix-ui/react-toast';
import { cn } from '@/lib/cn';
import { ToastContext, type ToastOptions, type ToastTone } from './toast-context';

interface ToastEntry extends ToastOptions {
  id: number;
  open: boolean;
}

const TOAST_DURATION_MS = 5000;

/* Accent stripe per tone, matching the prototype's toast styling. */
const toneAccent: Record<ToastTone, string> = {
  default: 'border-l-accent',
  success: 'border-l-success',
  danger: 'border-l-danger',
};

/* Imperative toast system on Radix Toast (swipe-to-dismiss, timers, hotkey, and aria-live region
   handled for us). The provider owns the toast list; `useToast().toast(...)` appends to it. Closed
   toasts are marked `open: false` (so Radix can animate out) and pruned on the close callback. */
export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastEntry[]>([]);
  const nextId = useRef(0);

  const toast = useCallback((options: ToastOptions) => {
    const id = nextId.current++;
    setToasts((current) => [...current, { ...options, id, open: true }]);
  }, []);

  const setOpen = useCallback((id: number, open: boolean) => {
    setToasts((current) =>
      open
        ? current.map((t) => (t.id === id ? { ...t, open } : t))
        : current.filter((t) => t.id !== id),
    );
  }, []);

  const value = useMemo(() => ({ toast }), [toast]);

  return (
    <ToastContext.Provider value={value}>
      <RadixToast.Provider duration={TOAST_DURATION_MS} swipeDirection="right">
        {children}

        {toasts.map((t) => (
          <RadixToast.Root
            key={t.id}
            open={t.open}
            onOpenChange={(open) => setOpen(t.id, open)}
            className={cn(
              'flex items-start gap-3 rounded-xl border border-l-4 border-border bg-elevated p-3.5 shadow-card',
              'data-[swipe=move]:translate-x-[var(--radix-toast-swipe-move-x)] data-[swipe=cancel]:translate-x-0',
              toneAccent[t.tone ?? 'default'],
            )}
          >
            <div className="flex-1 space-y-0.5">
              <RadixToast.Title className="text-sm font-semibold text-text">
                {t.title}
              </RadixToast.Title>
              {t.description && (
                <RadixToast.Description className="text-sm text-text-muted">
                  {t.description}
                </RadixToast.Description>
              )}
            </div>
            <RadixToast.Close
              aria-label="Close"
              className="flex h-6 w-6 shrink-0 items-center justify-center rounded-md text-text-muted transition-colors hover:bg-divider hover:text-text"
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <line x1="18" y1="6" x2="6" y2="18" />
                <line x1="6" y1="6" x2="18" y2="18" />
              </svg>
            </RadixToast.Close>
          </RadixToast.Root>
        ))}

        <RadixToast.Viewport className="fixed bottom-0 right-0 z-50 flex w-[24rem] max-w-[calc(100vw-2rem)] flex-col gap-2 p-4 outline-none" />
      </RadixToast.Provider>
    </ToastContext.Provider>
  );
}
