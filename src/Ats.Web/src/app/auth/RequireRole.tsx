import { Outlet } from 'react-router-dom';
import { useAuth } from './auth-context';
import { EmptyState } from '@/components/ui';
import type { Role } from '@/types/enums';

/* Shows an inline forbidden state instead of redirecting, which would be more confusing. */
export function RequireRole({ roles }: { roles: Role[] }) {
  const { role } = useAuth();

  if (role === null || !roles.includes(role)) {
    return (
      <div className="flex min-h-[60vh] items-center justify-center">
        <EmptyState
          title="You don’t have access to this page"
          description="Ask an administrator if you think this is a mistake."
        />
      </div>
    );
  }

  return <Outlet />;
}
