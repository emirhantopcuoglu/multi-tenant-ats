import type { HTMLAttributes, ReactNode, ThHTMLAttributes, TdHTMLAttributes } from 'react';
import { cn } from '@/lib/cn';

export type SortDirection = 'asc' | 'desc';

/* Composable parts instead of a config-driven data table. */

export function Table({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <div className={cn('overflow-hidden rounded-2xl border border-border bg-card shadow-card', className)}>
      <div className="overflow-x-auto">
        <table className="w-full border-collapse text-sm">{children}</table>
      </div>
    </div>
  );
}

export function THead({ children }: { children: ReactNode }) {
  return <thead className="bg-bg">{children}</thead>;
}

export function TBody({ children }: { children: ReactNode }) {
  return <tbody>{children}</tbody>;
}

interface TRProps extends HTMLAttributes<HTMLTableRowElement> {
  interactive?: boolean;
}

export function TR({ interactive = false, className, children, ...rest }: TRProps) {
  return (
    <tr
      className={cn(
        interactive && 'border-t border-divider transition-colors hover:bg-bg',
        className,
      )}
      {...rest}
    >
      {children}
    </tr>
  );
}

const headerCellClasses =
  'px-4.5 py-3 text-left text-xs font-semibold uppercase tracking-wide text-text-muted';

export function TH({ className, children, ...rest }: ThHTMLAttributes<HTMLTableCellElement>) {
  return (
    <th className={cn(headerCellClasses, className)} {...rest}>
      {children}
    </th>
  );
}

interface SortableTHProps {
  children: ReactNode;
  /* Undefined when this column isn't the active sort. */
  direction?: SortDirection;
  onSort: () => void;
  align?: 'left' | 'right';
}

export function SortableTH({ children, direction, onSort, align = 'left' }: SortableTHProps) {
  const ariaSort = direction === 'asc' ? 'ascending' : direction === 'desc' ? 'descending' : 'none';
  return (
    <th className={cn(headerCellClasses, align === 'right' && 'text-right')} aria-sort={ariaSort}>
      <button
        type="button"
        onClick={onSort}
        className={cn(
          'inline-flex items-center gap-1.5 font-semibold uppercase tracking-wide text-text-muted transition-colors hover:text-text focus-visible:outline-none focus-visible:text-text',
          align === 'right' && 'flex-row-reverse',
        )}
      >
        {children}
        <svg
          width="13"
          height="13"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2.5"
          strokeLinecap="round"
          strokeLinejoin="round"
          className={cn('transition-opacity', direction ? 'opacity-100' : 'opacity-30')}
        >
          {direction === 'asc' ? (
            <polyline points="18 15 12 9 6 15" />
          ) : (
            <polyline points="6 9 12 15 18 9" />
          )}
        </svg>
      </button>
    </th>
  );
}

export function TD({ className, children, ...rest }: TdHTMLAttributes<HTMLTableCellElement>) {
  return (
    <td className={cn('px-4.5 py-3 text-text', className)} {...rest}>
      {children}
    </td>
  );
}

export function TableFooter({ children }: { children: ReactNode }) {
  return (
    <div className="flex items-center justify-between border-t border-divider bg-bg px-4.5 py-3">
      {children}
    </div>
  );
}
