import { Link } from 'react-router-dom';
import { Card } from '@/components/ui';

/* Placeholder register route; the real workspace-creation form lands in Step 2.2. */
export function RegisterPage() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-bg p-6">
      <Card className="w-full max-w-sm space-y-3 text-center">
        <h1 className="text-xl font-semibold text-text">Create your workspace</h1>
        <p className="text-sm text-text-muted">The registration form arrives in the next step.</p>
        <p className="text-sm text-text-muted">
          Already have an account?{' '}
          <Link to="/login" className="text-accent hover:underline">
            Sign in
          </Link>
        </p>
      </Card>
    </div>
  );
}
