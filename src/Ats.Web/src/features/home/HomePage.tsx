import { Link } from 'react-router-dom';
import { useAuth } from '@/app/auth/auth-context';
import { Badge, Button, Card } from '@/components/ui';
import { ThemeToggle } from '@/components/ThemeToggle';
import { LanguageSwitcher } from '@/components/LanguageSwitcher';

/* Placeholder protected landing page. It proves the auth foundation end to end (it can only render
   for an authenticated user, and it reads the profile from AuthContext). The real app shell and
   dashboard replace it in Steps 2.3 / 4.1. */
export function HomePage() {
  const { user, role, logout } = useAuth();

  return (
    <div className="min-h-screen bg-bg text-text">
      <header className="flex items-center justify-between border-b border-border px-6 py-4">
        <h1 className="text-lg font-semibold tracking-tight">Ats</h1>
        <div className="flex items-center gap-2">
          <LanguageSwitcher />
          <ThemeToggle />
          <Button variant="secondary" onClick={() => logout()}>
            Log out
          </Button>
        </div>
      </header>

      <main className="mx-auto max-w-2xl space-y-4 p-6">
        <Card className="space-y-2">
          <p className="text-sm text-text-muted">Signed in as</p>
          <p className="text-lg font-semibold">
            {user?.firstName} {user?.lastName}
          </p>
          <div className="flex items-center gap-2 text-sm text-text-muted">
            <span>{user?.email}</span>
            {role && <Badge tone="accent">{role}</Badge>}
          </div>
          <p className="text-sm text-text-muted">Workspace: {user?.tenant.companyName}</p>
        </Card>
        <p className="text-sm text-text-muted">
          This placeholder is replaced by the app shell and dashboard in later steps.{' '}
          <Link to="/playground" className="text-accent hover:underline">
            Open the component playground
          </Link>
        </p>
      </main>
    </div>
  );
}
