import { Outlet } from 'react-router-dom';
import { useAuth } from './auth-context';
import { EmptyState } from '@/components/ui';
import type { Role } from '@/types/enums';

/* Role gate, nested inside RequireAuth (so a user is guaranteed present). When the user's role isn't
   in `roles`, we render an inline forbidden state rather than redirecting — the route exists, the
   user simply lacks permission, and a silent redirect would be more confusing than an explanation. */
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
