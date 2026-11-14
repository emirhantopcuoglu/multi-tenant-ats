import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from './auth-context';

/* Nested inside RequireCandidateAuth: a frozen account, or one waiting for deletion, can sign in but
   only reach its own screen. Being in the route tree covers new candidate routes by default. */
export function RequireActiveCandidate() {
  const { user } = useAuth();

  if (user?.kind === 'candidate' && user.status === 'Frozen') {
    return <Navigate to="/candidate/reactivate" replace />;
  }

  if (user?.kind === 'candidate' && user.status === 'PendingDeletion') {
    return <Navigate to="/candidate/cancel-deletion" replace />;
  }

  return <Outlet />;
}
