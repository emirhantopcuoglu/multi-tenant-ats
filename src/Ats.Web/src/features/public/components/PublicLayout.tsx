import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ThemeToggle } from '@/components/ThemeToggle';
import { LanguageSwitcher } from '@/components/LanguageSwitcher';
import { useAuth } from '@/app/auth/auth-context';
import { NotificationBell } from '@/features/notifications/components/NotificationBell';
import { cn } from '@/lib/cn';

/* Chrome for anonymous careers and marketplace pages: a slim branded header with theme + language
   toggles, a candidate auth CTA (sign in / register when anonymous, name + sign out when a
   candidate is active), a centered content column, and a shared footer.

   `wide` widens the content column for landing-style pages (the marketplace homepage); the
   narrower default stays right for reading-focused pages like a job description. The header and
   footer follow the same width so the page edges stay aligned. */
export function PublicLayout({ children, wide = false }: { children: ReactNode; wide?: boolean }) {
  const { t } = useTranslation();
  const { user, logout } = useAuth();
  const containerWidth = wide ? 'max-w-6xl' : 'max-w-4xl';

  return (
    <div className="flex min-h-screen flex-col bg-bg text-text">
      <header className="border-b border-border">
        <div className={cn('mx-auto flex items-center justify-between px-6 py-4', containerWidth)}>
          <Link to="/" className="flex items-center gap-2">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-accent text-sm font-bold text-accent-fg">
              A
            </span>
            <span className="font-semibold">{t('common.appName')}</span>
          </Link>
          <div className="flex items-center gap-3">
            <LanguageSwitcher />
            <ThemeToggle />

            {user?.kind === 'candidate' ? (
              <div className="flex items-center gap-3">
                <NotificationBell />
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
                <Link to="/login" className="text-sm text-text-muted hover:text-text">
                  {t('public.forCompanies')}
                </Link>
                <span aria-hidden="true" className="h-4 w-px bg-border" />
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

      <main className={cn('mx-auto w-full flex-1 px-6 py-10', containerWidth)}>{children}</main>

      <footer className="border-t border-border">
        <div className={cn('mx-auto space-y-8 px-6 py-10', containerWidth)}>
          <div className="grid gap-8 sm:grid-cols-3">
            <div className="space-y-2">
              <div className="flex items-center gap-2">
                <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-accent text-sm font-bold text-accent-fg">
                  A
                </span>
                <span className="font-semibold">{t('common.appName')}</span>
              </div>
              <p className="text-sm text-text-muted">{t('public.footer.tagline')}</p>
            </div>

            <nav aria-label={t('public.footer.candidatesHeading')} className="space-y-2">
              <h2 className="text-sm font-semibold text-text">
                {t('public.footer.candidatesHeading')}
              </h2>
              <ul className="space-y-1.5 text-sm text-text-muted">
                <li>
                  <Link to="/" className="hover:text-text">
                    {t('public.footer.browseJobs')}
                  </Link>
                </li>
                <li>
                  <Link to="/candidate/login" className="hover:text-text">
                    {t('public.marketplace.seekerSignIn')}
                  </Link>
                </li>
                <li>
                  <Link to="/candidate/register" className="hover:text-text">
                    {t('public.marketplace.seekerRegister')}
                  </Link>
                </li>
              </ul>
            </nav>

            <nav aria-label={t('public.footer.companiesHeading')} className="space-y-2">
              <h2 className="text-sm font-semibold text-text">
                {t('public.footer.companiesHeading')}
              </h2>
              <ul className="space-y-1.5 text-sm text-text-muted">
                <li>
                  <Link to="/login" className="hover:text-text">
                    {t('public.marketplace.hireSignIn')}
                  </Link>
                </li>
                <li>
                  <Link to="/register" className="hover:text-text">
                    {t('public.marketplace.hireRegister')}
                  </Link>
                </li>
              </ul>
            </nav>
          </div>

          <p className="border-t border-border pt-6 text-xs text-text-muted">
            {t('public.footer.copyright', { year: new Date().getFullYear() })}
          </p>
        </div>
      </footer>
    </div>
  );
}
