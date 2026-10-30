import type { ReactNode } from 'react';
import { cn } from '@/lib/cn';

interface EmptyStateProps {
  icon?: ReactNode;
  title: ReactNode;
  description?: ReactNode;
  action?: ReactNode;
  className?: string;
}

export function EmptyState({ icon, title, description, action, className }: EmptyStateProps) {
  return (
    <div className={cn('flex flex-col items-center justify-center gap-3 px-6 py-12 text-center', className)}>
      {icon && <div className="text-text-muted">{icon}</div>}
      <div className="space-y-1">
        <h3 className="text-sm font-semibold text-text">{title}</h3>
        {description && <p className="text-sm text-text-muted">{description}</p>}
      </div>
      {action}
    </div>
  );
}
