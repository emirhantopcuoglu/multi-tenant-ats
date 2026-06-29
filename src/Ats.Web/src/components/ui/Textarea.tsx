import { forwardRef, type TextareaHTMLAttributes } from 'react';
import { cn } from '@/lib/cn';

interface TextareaProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  invalid?: boolean;
}

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaProps>(function Textarea(
  { invalid = false, className, rows = 3, ...rest },
  ref,
) {
  return (
    <textarea
      ref={ref}
      rows={rows}
      aria-invalid={invalid || undefined}
      className={cn(
        'w-full resize-y rounded-lg border bg-bg px-3 py-2.5 text-sm leading-relaxed text-text outline-none transition-shadow placeholder:text-text-muted disabled:cursor-not-allowed disabled:bg-divider disabled:text-text-disabled',
        invalid
          ? 'border-danger focus:ring-3 focus:ring-danger-bg'
          : 'border-border focus:border-accent focus:ring-3 focus:ring-accent-subtle',
        className,
      )}
      {...rest}
    />
  );
});
