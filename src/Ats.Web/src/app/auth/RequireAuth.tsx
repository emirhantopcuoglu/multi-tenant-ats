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

/* Company areas. Anonymous users go to /login; candidates go to the marketplace. */
export function RequireAuth() {
  const { isAuthenticated, isLoading, user } = useAuth();
  const location = useLocation();

  if (isLoading) {
    return <FullPageSpinner />;
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  if (user?.kind === 'candidate') {
    return <Navigate to="/" replace />;
  }

  return <Outlet />;
}
