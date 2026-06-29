import { useTranslation } from 'react-i18next';
import { ThemeToggle } from '@/components/ThemeToggle';
import { LanguageSwitcher } from '@/components/LanguageSwitcher';

// Temporary token + i18n showcase (Steps 1.2–1.3). It proves the design tokens flip light/dark and
// that switching language re-renders every visible string while enum *values* stay English in data.
// Real routing and screens replace this in later steps.
export default function App() {
  const { t } = useTranslation();

  // Enum values as they arrive from the API (English); only the visible label is translated.
  const jobStatuses = ['Draft', 'Published', 'Closed', 'Archived'] as const;

  return (
    <div className="min-h-screen bg-bg text-text">
      <header className="flex items-center justify-between border-b border-border px-6 py-4">
        <h1 className="text-2xl font-semibold tracking-tight">{t('common.appName')}</h1>
        <div className="flex items-center gap-2">
          <LanguageSwitcher />
          <ThemeToggle />
        </div>
      </header>

      <main className="mx-auto max-w-3xl space-y-6 p-6">
        <section className="space-y-1">
          <h2 className="text-lg font-semibold">{t('nav.overview')}</h2>
          <p className="text-text-muted">{t('empty.body')}</p>
        </section>

        <div className="rounded-xl border border-border bg-card p-5 shadow-card">
          <p className="mb-3 text-sm text-text-muted">
            {t('table.status')} ({t('table.rows', { count: 148 })})
          </p>
          <div className="flex flex-wrap gap-2">
            {jobStatuses.map((value) => (
              <span
                key={value}
                className="rounded-full bg-accent-subtle px-3 py-1 text-sm font-medium text-accent"
              >
                {/* value stays English; label is localized via the status namespace */}
                {t(`status.${value}`)}
              </span>
            ))}
          </div>
        </div>

        <button
          type="button"
          className="rounded-lg bg-accent px-4 py-2 font-medium text-accent-fg transition-colors hover:bg-accent-hover"
        >
          {t('auth.signInBtn')}
        </button>
      </main>
    </div>
  );
}
