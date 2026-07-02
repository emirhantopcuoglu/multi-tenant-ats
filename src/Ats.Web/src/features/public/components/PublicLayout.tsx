import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ThemeToggle } from '@/components/ThemeToggle';
import { LanguageSwitcher } from '@/components/LanguageSwitcher';
import { useAuth } from '@/app/auth/auth-context';

/* Chrome for anonymous careers and marketplace pages: a slim branded header with theme + language
   toggles, a candidate auth CTA (sign in / register when anonymous, name + sign out when a
   candidate is active), and a centered content column. */
export function PublicLayout({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  const { user, logout } = useAuth();

  return (
    <div className="flex min-h-screen flex-col bg-bg text-text">
      <header className="border-b border-border">
        <div className="mx-auto flex max-w-4xl items-center justify-between px-6 py-4">
          <div className="flex items-center gap-2">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-accent text-sm font-bold text-accent-fg">
              A
            </span>
            <span className="font-semibold">{t('common.appName')}</span>
          </div>
          <div className="flex items-center gap-3">
            <LanguageSwitcher />
            <ThemeToggle />

            {user?.kind === 'candidate' ? (
              <div className="flex items-center gap-3">
                <Link
                  to="/candidate/applications"
                  className="text-sm text-text-muted hover:text-text"
                >
                  {t('candidateAuth.myApplications')}
                </Link>
                <span className="text-sm font-medium text-text">{user.firstName}</span>
                <button
                  type="button"
                  onClick={() => void logout()}
                  className="text-sm text-text-muted hover:text-text"
                >
                  {t('common.signOut')}
                </button>
              </div>
            ) : !user ? (
              <div className="flex items-center gap-3">
                <Link
                  to="/candidate/login"
                  className="text-sm text-text-muted hover:text-text"
                >
                  {t('candidateAuth.publicSignIn')}
                </Link>
                <Link
                  to="/candidate/register"
                  className="rounded-lg bg-accent px-3 py-1.5 text-sm font-medium text-accent-fg hover:bg-accent-hover"
                >
                  {t('candidateAuth.publicRegister')}
                </Link>
              </div>
            ) : null}
          </div>
        </div>
      </header>

      <main className="mx-auto w-full max-w-4xl flex-1 px-6 py-10">{children}</main>
    </div>
  );
}
