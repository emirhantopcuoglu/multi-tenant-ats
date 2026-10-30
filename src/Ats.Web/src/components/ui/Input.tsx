import { forwardRef, type InputHTMLAttributes } from 'react';
import { cn } from '@/lib/cn';

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  /* Pair with a message via aria-describedby. */
  invalid?: boolean;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(function Input(
  { invalid = false, className, ...rest },
  ref,
) {
  return (
    <input
      ref={ref}
      aria-invalid={invalid || undefined}
      className={cn(
        'h-9.5 w-full rounded-lg border bg-bg px-3 text-sm text-text outline-none transition-shadow placeholder:text-text-muted disabled:cursor-not-allowed disabled:bg-divider disabled:text-text-disabled',
        invalid
          ? 'border-danger focus:ring-3 focus:ring-danger-bg'
          : 'border-border focus:border-accent focus:ring-3 focus:ring-accent-subtle',
        className,
      )}
      {...rest}
    />
  );
});
