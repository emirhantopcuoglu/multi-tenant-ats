import { forwardRef, type SelectHTMLAttributes } from 'react';
import { cn } from '@/lib/cn';

interface SelectProps extends SelectHTMLAttributes<HTMLSelectElement> {
  invalid?: boolean;
}

/* A styled native <select>: we keep the native element (free accessibility + mobile pickers) and
   only restyle it. `appearance-none` removes the OS arrow so we can render our own chevron, which
   sits in an absolutely-positioned span over the right padding. */
export const Select = forwardRef<HTMLSelectElement, SelectProps>(function Select(
  { invalid = false, className, children, ...rest },
  ref,
) {
  return (
    <div className="relative">
      <select
        ref={ref}
        aria-invalid={invalid || undefined}
        className={cn(
          'h-9.5 w-full cursor-pointer appearance-none rounded-lg border bg-bg pl-3 pr-9 text-sm text-text outline-none transition-shadow disabled:cursor-not-allowed disabled:bg-divider disabled:text-text-disabled',
          invalid
            ? 'border-danger focus:ring-3 focus:ring-danger-bg'
            : 'border-border focus:border-accent focus:ring-3 focus:ring-accent-subtle',
          className,
        )}
        {...rest}
      >
        {children}
      </select>
      <span
        aria-hidden="true"
        className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-text-muted"
      >
        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <polyline points="6 9 12 15 18 9" />
        </svg>
      </span>
    </div>
  );
});
