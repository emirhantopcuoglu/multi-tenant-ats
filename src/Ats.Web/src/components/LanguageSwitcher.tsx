import { useTranslation } from 'react-i18next';
import { SUPPORTED_LANGUAGES, type Language } from '@/i18n';

/* Segmented EN/TR control, matching the design prototype's language toggle. Persistence is handled
   by the i18n layer (languageChanged listener), so this component only flips the active language. */
export function LanguageSwitcher() {
  const { i18n } = useTranslation();
  const current = i18n.resolvedLanguage as Language;

  return (
    <div
      role="group"
      aria-label="Language"
      className="flex gap-0.5 rounded-lg border border-border bg-bg p-0.5"
    >
      {SUPPORTED_LANGUAGES.map((language) => {
        const isActive = current === language;
        return (
          <button
            key={language}
            type="button"
            onClick={() => i18n.changeLanguage(language)}
            aria-pressed={isActive}
            className={
              'rounded-md px-2.5 py-1 text-xs font-semibold uppercase transition-colors ' +
              (isActive
                ? 'bg-card text-accent shadow-card'
                : 'text-text-muted hover:text-text')
            }
          >
            {language}
          </button>
        );
      })}
    </div>
  );
}
