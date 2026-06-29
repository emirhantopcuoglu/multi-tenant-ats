import type { HTMLAttributes, ReactNode } from 'react';
import { cn } from '@/lib/cn';

interface KanbanColumnProps {
  title: ReactNode;
  /** Item count shown as a pill next to the title. */
  count?: number;
  children: ReactNode;
}

/* Presentational pipeline column. Drag-and-drop wiring (dnd-kit) is added with the Applications
   Kanban board (Step 3.4); here we only provide the column shell and card styling. */
export function KanbanColumn({ title, count, children }: KanbanColumnProps) {
  return (
    <div className="flex w-72 shrink-0 flex-col gap-2.5 rounded-2xl border border-border bg-bg p-3">
      <div className="flex items-center justify-between px-1">
        <span className="text-sm font-semibold text-text">{title}</span>
        {count != null && (
          <span className="rounded-full border border-border bg-card px-2 py-0.5 text-xs font-semibold text-text-muted">
            {count}
          </span>
        )}
      </div>
      <div className="flex flex-col gap-2.5">{children}</div>
    </div>
  );
}

interface KanbanCardProps extends HTMLAttributes<HTMLDivElement> {
  children: ReactNode;
}

export function KanbanCard({ className, children, ...rest }: KanbanCardProps) {
  return (
    <div
      className={cn(
        'flex flex-col gap-2 rounded-xl border border-border bg-card p-3 shadow-card transition-colors hover:border-accent',
        className,
      )}
      {...rest}
    >
      {children}
    </div>
  );
}
