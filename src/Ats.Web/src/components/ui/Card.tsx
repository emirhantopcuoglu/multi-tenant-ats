import type { HTMLAttributes } from 'react';
import { cn } from '@/lib/cn';

interface CardProps extends HTMLAttributes<HTMLDivElement> {
  /** Apply default inner padding. Turn off for cards that own their own layout (e.g. tables). */
  padded?: boolean;
}

/* The standard elevated surface: card background, hairline border, and the design's shadow token. */
export function Card({ padded = true, className, children, ...rest }: CardProps) {
  return (
    <div
      className={cn(
        'rounded-2xl border border-border bg-card shadow-card',
        padded && 'p-5',
        className,
      )}
      {...rest}
    >
      {children}
    </div>
  );
}
