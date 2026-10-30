import type { ReactNode } from 'react';
import { Card } from './Card';

interface StatCardProps {
  label: ReactNode;
  value: ReactNode;
  hint?: ReactNode;
  icon?: ReactNode;
}

export function StatCard({ label, value, hint, icon }: StatCardProps) {
  return (
    <Card className="flex flex-col gap-2">
      <div className="flex items-center justify-between">
        <span className="text-sm text-text-muted">{label}</span>
        {icon && <span className="text-text-muted">{icon}</span>}
      </div>
      <span className="text-2xl font-semibold tracking-tight text-text">{value}</span>
      {hint && <span className="text-xs text-text-muted">{hint}</span>}
    </Card>
  );
}
