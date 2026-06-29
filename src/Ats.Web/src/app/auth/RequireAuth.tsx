import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from './auth-context';

/* Centered spinner shown while the initial session check runs, so guarded routes don't flash the
   login screen on reload before /auth/me resolves. */
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

/* Gate for authenticated areas. Used as a layout route wrapping protected children via <Outlet>.
   Unauthenticated users are sent to /login, remembering where they came from so login can return
   them there. */
export function RequireAuth() {
  const { isAuthenticated, isLoading } = useAuth();
  const location = useLocation();

  if (isLoading) {
    return <FullPageSpinner />;
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  return <Outlet />;
}
