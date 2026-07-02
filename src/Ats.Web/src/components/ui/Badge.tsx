import type { ReactNode } from 'react';
import { cn } from '@/lib/cn';
import type { BadgeTone } from '@/lib/statusColors';

/* Tone → classes, ported from the prototype's `pill(kind)` function. Screens map a status enum to a
   tone via the maps in lib/statusColors, then pass it here — keeping colour decisions in one place. */
const toneClasses: Record<BadgeTone, string> = {
  neutral: 'bg-divider text-text-muted ring-1 ring-inset ring-border',
  gray: 'bg-divider text-text-disabled',
  accent: 'bg-accent-subtle text-accent',
  success: 'bg-success-bg text-success',
  warning: 'bg-warning-bg text-warning',
  danger: 'bg-danger-bg text-danger',
  info: 'bg-info-bg text-info',
  solidDanger: 'bg-danger text-white',
  solidSuccess: 'bg-success text-white',
};

/* Dot colour for tones where the prototype shows a leading status dot (e.g. Published). */
const dotColor: Partial<Record<BadgeTone, string>> = {
  success: 'bg-success',
  accent: 'bg-accent',
  danger: 'bg-danger',
  warning: 'bg-warning',
  info: 'bg-info',
};

interface BadgeProps {
  tone?: BadgeTone;
  /** Show a small leading status dot. */
  dot?: boolean;
  children: ReactNode;
  className?: string;
}

export function Badge({ tone = 'neutral', dot = false, children, className }: BadgeProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 whitespace-nowrap rounded-full px-2.5 py-0.5 text-xs font-medium leading-snug',
        toneClasses[tone],
        className,
      )}
    >
      {dot && <span className={cn('h-1.5 w-1.5 rounded-full', dotColor[tone] ?? 'bg-current')} />}
      {children}
    </span>
  );
}
