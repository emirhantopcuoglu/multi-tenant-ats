import type { ReactNode } from 'react';
import { cn, focusRing } from '@/lib/cn';

interface SidebarNavItemProps {
  icon?: ReactNode;
  label: ReactNode;
  active?: boolean;
  onClick?: () => void;
}

/* A single sidebar navigation entry. Active state uses the accent-subtle surface; rendered as a
   button for now (the router that turns these into links arrives in Step 2.1). `aria-current`
   exposes the active page to assistive tech. */
export function SidebarNavItem({ icon, label, active = false, onClick }: SidebarNavItemProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-current={active ? 'page' : undefined}
      className={cn(
        'flex w-full items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
        active ? 'bg-accent-subtle text-accent' : 'text-text-muted hover:bg-divider hover:text-text',
        focusRing,
      )}
    >
      {icon && <span className="shrink-0">{icon}</span>}
      {label}
    </button>
  );
}
