import { forwardRef, type InputHTMLAttributes, type ReactNode } from 'react';
import { cn } from '@/lib/cn';

interface ToggleProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  label?: ReactNode;
}

/* Switch built on a native checkbox (role="switch" for assistive tech). Track and knob are styled
   siblings of the hidden input, so `peer-checked` flips the track colour and slides the knob. */
export const Toggle = forwardRef<HTMLInputElement, ToggleProps>(function Toggle(
  { label, className, ...rest },
  ref,
) {
  return (
    <label className={cn('inline-flex cursor-pointer select-none items-center gap-2', className)}>
      <span className="relative inline-flex">
        <input ref={ref} type="checkbox" role="switch" className="peer sr-only" {...rest} />
        <span className="block h-5 w-9 rounded-full bg-border transition-colors peer-checked:bg-accent peer-focus-visible:ring-2 peer-focus-visible:ring-accent peer-focus-visible:ring-offset-2 peer-focus-visible:ring-offset-bg peer-disabled:opacity-50" />
        <span className="pointer-events-none absolute left-0.5 top-0.5 h-4 w-4 rounded-full bg-white shadow-sm transition-transform peer-checked:translate-x-4" />
      </span>
      {label != null && <span className="text-sm text-text">{label}</span>}
    </label>
  );
});
