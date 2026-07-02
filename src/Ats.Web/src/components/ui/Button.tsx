import type { ButtonHTMLAttributes, ReactNode } from 'react';
import { cn, focusRing } from '@/lib/cn';

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';

/* Variant → classes. Plain object instead of a variants library (cva): the set is small and
   readable, and avoids a dependency for what is just a lookup. */
const variantClasses: Record<ButtonVariant, string> = {
  primary: 'bg-accent text-accent-fg border-transparent hover:bg-accent-hover',
  secondary: 'bg-card text-text border-border hover:bg-divider',
  ghost: 'bg-transparent text-text border-transparent hover:bg-divider',
  danger: 'bg-danger text-white border-transparent hover:brightness-110',
};

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  /** Icon rendered before the label (e.g. a plus). */
  leadingIcon?: ReactNode;
}

export function Button({
  variant = 'primary',
  leadingIcon,
  className,
  children,
  type = 'button',
  ...rest
}: ButtonProps) {
  return (
    <button
      // Default to type="button" so a button inside a form doesn't submit it by accident.
      type={type}
      className={cn(
        'inline-flex h-9.5 items-center justify-center gap-1.5 rounded-lg border px-4 text-sm font-medium transition-colors disabled:cursor-not-allowed disabled:border-border disabled:bg-divider disabled:text-text-disabled disabled:hover:bg-divider',
        variantClasses[variant],
        focusRing,
        className,
      )}
      {...rest}
    >
      {leadingIcon}
      {children}
    </button>
  );
}
