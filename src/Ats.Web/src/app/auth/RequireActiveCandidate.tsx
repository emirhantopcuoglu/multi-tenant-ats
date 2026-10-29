import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from './auth-context';

/* Nested inside RequireCandidateAuth: a frozen account can sign in but only reach the reactivation
   screen. Being in the route tree covers new candidate routes by default. */
export function RequireActiveCandidate() {
  const { user } = useAuth();

  if (user?.kind === 'candidate' && user.status === 'Frozen') {
    return <Navigate to="/candidate/reactivate" replace />;
  }

  return <Outlet />;
}
