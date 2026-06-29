import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { ThemeToggle } from '@/components/ThemeToggle';
import { LanguageSwitcher } from '@/components/LanguageSwitcher';

/* Split-screen auth shell (form left, brand panel right), matching Auth.dc.html. The brand panel is
   hidden below lg so small screens get a focused single column. Theme + language toggles stay
   visible, as in the prototype. */
export function AuthLayout({
  title,
  subtitle,
  children,
}: {
  title: ReactNode;
  subtitle: ReactNode;
  children: ReactNode;
}) {
  const { t } = useTranslation();

  return (
    <div className="flex min-h-screen bg-bg text-text">
      <div className="flex w-full flex-col lg:w-1/2">
        <header className="flex items-center justify-between p-6">
          <div className="flex items-center gap-2">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-accent text-sm font-bold text-accent-fg">
              A
            </span>
            <span className="font-semibold">{t('common.appName')}</span>
          </div>
          <div className="flex items-center gap-2">
            <LanguageSwitcher />
            <ThemeToggle />
          </div>
        </header>

        <div className="flex flex-1 items-center justify-center px-6 pb-12">
          <div className="w-full max-w-sm space-y-6">
            <div className="space-y-1">
              <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
              <p className="text-sm text-text-muted">{subtitle}</p>
            </div>
            {children}
          </div>
        </div>
      </div>

      <aside className="hidden flex-col justify-center bg-accent p-12 text-accent-fg lg:flex lg:w-1/2">
        <span className="mb-6 inline-flex w-fit rounded-full bg-white/15 px-3 py-1 text-xs font-medium">
          {t('auth.tagline')}
        </span>
        <h2 className="max-w-md text-3xl font-semibold tracking-tight">{t('auth.brandHead')}</h2>
        <p className="mt-4 max-w-md text-sm text-accent-fg/80">{t('auth.brandSub')}</p>
      </aside>
    </div>
  );
}
