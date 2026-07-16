import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from './auth-context';

/* Second gate nested inside RequireCandidateAuth: a frozen account may sign in (locked product
   decision), but the only candidate screen it should reach is the reactivation one. Living in the
   route tree — not in each page — means new candidate routes are covered by default. */
export function RequireActiveCandidate() {
  const { user } = useAuth();

  if (user?.kind === 'candidate' && user.status === 'Frozen') {
    return <Navigate to="/candidate/reactivate" replace />;
  }

  return <Outlet />;
}
