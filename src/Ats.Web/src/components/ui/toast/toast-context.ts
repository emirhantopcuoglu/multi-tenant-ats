import { createContext, useContext, type ReactNode } from 'react';

export type ToastTone = 'default' | 'success' | 'danger';

export interface ToastOptions {
  title: ReactNode;
  description?: ReactNode;
  tone?: ToastTone;
}

export interface ToastContextValue {
  toast: (options: ToastOptions) => void;
}

/* Kept separate from the provider component so React Fast Refresh keeps its boundary. */
export const ToastContext = createContext<ToastContextValue | null>(null);

export function useToast(): ToastContextValue {
  const context = useContext(ToastContext);
  if (context === null) {
    throw new Error('useToast must be used within a ToastProvider');
  }
  return context;
}
