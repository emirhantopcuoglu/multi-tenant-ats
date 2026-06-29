import { Link } from 'react-router-dom';
import { Card } from '@/components/ui';

/* Placeholder login route. The real split-screen form wired to POST /auth/login arrives in Step 2.2;
   this exists so the router skeleton and the unauthenticated redirect target are in place. */
export function LoginPage() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-bg p-6">
      <Card className="w-full max-w-sm space-y-3 text-center">
        <h1 className="text-xl font-semibold text-text">Sign in</h1>
        <p className="text-sm text-text-muted">The login form arrives in the next step.</p>
        <p className="text-sm text-text-muted">
          New to Ats?{' '}
          <Link to="/register" className="text-accent hover:underline">
            Create a workspace
          </Link>
        </p>
      </Card>
    </div>
  );
}
