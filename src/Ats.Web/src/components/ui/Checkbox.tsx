import { forwardRef, type InputHTMLAttributes, type ReactNode } from 'react';
import { cn } from '@/lib/cn';

interface CheckboxProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  label?: ReactNode;
}

/* A native checkbox kept for accessibility (visually hidden via `peer sr-only`) with a styled box
   driven by `peer-checked`. The check icon lives inside the box and is revealed with the arbitrary
   variant `peer-checked:[&_svg]:opacity-100` — needed because the icon is a descendant of the box,
   not a following sibling, so a plain `peer-checked:` on the icon wouldn't match. */
export const Checkbox = forwardRef<HTMLInputElement, CheckboxProps>(function Checkbox(
  { label, className, ...rest },
  ref,
) {
  return (
    <label className={cn('inline-flex cursor-pointer select-none items-center gap-2', className)}>
      <span className="relative inline-flex">
        <input ref={ref} type="checkbox" className="peer sr-only" {...rest} />
        <span className="flex h-4.5 w-4.5 items-center justify-center rounded-[5px] border border-border bg-bg transition-colors peer-checked:border-accent peer-checked:bg-accent peer-checked:[&_svg]:opacity-100 peer-focus-visible:ring-2 peer-focus-visible:ring-accent peer-focus-visible:ring-offset-2 peer-focus-visible:ring-offset-bg peer-disabled:cursor-not-allowed peer-disabled:opacity-50">
          <svg className="opacity-0 transition-opacity" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="20 6 9 17 4 12" />
          </svg>
        </span>
      </span>
      {label != null && <span className="text-sm text-text">{label}</span>}
    </label>
  );
});
