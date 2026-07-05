import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from './auth-context';

function FullPageSpinner() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-bg">
      <span
        role="status"
        aria-label="Loading"
        className="h-7 w-7 animate-spin rounded-full border-2 border-border border-t-accent"
      />
    </div>
  );
}

/* Gate for candidate-authenticated areas (applications, notifications, profile). Anonymous visitors
   are sent to /candidate/login remembering their intended destination; company users are redirected
   to /dashboard since they have no candidate identity to view these pages with. */
export function RequireCandidateAuth() {
  const { isAuthenticated, isLoading, user } = useAuth();
  const location = useLocation();

  if (isLoading) {
    return <FullPageSpinner />;
  }

  if (!isAuthenticated) {
    return <Navigate to="/candidate/login" replace state={{ from: location }} />;
  }

  if (user?.kind !== 'candidate') {
    return <Navigate to="/dashboard" replace />;
  }

  return <Outlet />;
}
