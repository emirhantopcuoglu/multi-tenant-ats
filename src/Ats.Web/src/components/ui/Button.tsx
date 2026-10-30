import type { ButtonHTMLAttributes, ReactNode } from 'react';
import { cn, focusRing } from '@/lib/cn';

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';

const variantClasses: Record<ButtonVariant, string> = {
  primary: 'bg-accent text-accent-fg border-transparent hover:bg-accent-hover',
  secondary: 'bg-card text-text border-border hover:bg-divider',
  ghost: 'bg-transparent text-text border-transparent hover:bg-divider',
  danger: 'bg-danger text-white border-transparent hover:brightness-110',
};

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
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
      // type="button" by default so it doesn't submit a surrounding form.
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
