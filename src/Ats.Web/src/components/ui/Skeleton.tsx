import { cn } from '@/lib/cn';

/* A pulsing placeholder block for loading states. Decorative, so it's hidden from assistive tech;
   the surrounding region should expose an aria-busy/loading label instead. */
export function Skeleton({ className }: { className?: string }) {
  return <div aria-hidden="true" className={cn('animate-pulse rounded-md bg-divider', className)} />;
}
