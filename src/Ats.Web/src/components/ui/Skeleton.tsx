import { cn } from '@/lib/cn';

/* Hidden from assistive tech; the surrounding region should carry the loading label. */
export function Skeleton({ className }: { className?: string }) {
  return <div aria-hidden="true" className={cn('animate-pulse rounded-md bg-divider', className)} />;
}
