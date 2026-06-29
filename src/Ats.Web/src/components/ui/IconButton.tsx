import type { ButtonHTMLAttributes, ReactNode } from 'react';
import { cn, focusRing } from '@/lib/cn';

export type IconButtonTone = 'default' | 'danger';

const toneClasses: Record<IconButtonTone, string> = {
  default: 'text-text hover:bg-divider',
  danger: 'text-danger hover:bg-danger-bg',
};

interface IconButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  /** Required: an icon button has no text, so it must carry its own accessible name. */
  'aria-label': string;
  icon: ReactNode;
  tone?: IconButtonTone;
}

export function IconButton({
  icon,
  tone = 'default',
  className,
  type = 'button',
  ...rest
}: IconButtonProps) {
  return (
    <button
      type={type}
      className={cn(
        'inline-flex h-9 w-9 items-center justify-center rounded-lg border border-border bg-card transition-colors disabled:cursor-not-allowed disabled:text-text-disabled',
        toneClasses[tone],
        focusRing,
        className,
      )}
      {...rest}
    >
      {icon}
    </button>
  );
}
