import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import en from './en.json';
import tr from './tr.json';

export const SUPPORTED_LANGUAGES = ['en', 'tr'] as const;
export type Language = (typeof SUPPORTED_LANGUAGES)[number];

/* Shared with the rest of the app; mirrors the THEME_STORAGE_KEY convention so language and
   theme persistence read the same way. */
export const LANGUAGE_STORAGE_KEY = 'ats-lang';

const FALLBACK_LANGUAGE: Language = 'en';

function isSupported(value: string | null): value is Language {
  return value !== null && (SUPPORTED_LANGUAGES as readonly string[]).includes(value);
}

/* Precedence: an explicit saved choice wins; otherwise honour the browser language if we ship it;
   otherwise fall back to English. Reads storage defensively (can throw in private mode). */
function getInitialLanguage(): Language {
  try {
    const saved = localStorage.getItem(LANGUAGE_STORAGE_KEY);
    if (isSupported(saved)) {
      return saved;
    }
  } catch {
    // Ignore storage errors and fall through to the browser/fallback language.
  }

  const browser = navigator.language.split('-')[0];
  return isSupported(browser) ? browser : FALLBACK_LANGUAGE;
}

i18n.use(initReactI18next).init({
  resources: {
    en: { translation: en },
    tr: { translation: tr },
  },
  lng: getInitialLanguage(),
  fallbackLng: FALLBACK_LANGUAGE,
  supportedLngs: SUPPORTED_LANGUAGES,
  interpolation: {
    // React escapes values during render, so i18next must not double-escape them.
    escapeValue: false,
  },
});

// Persist the choice whenever it changes, so it survives a reload. Kept out of components on
// purpose: the side effect belongs to the i18n layer, not the UI.
i18n.on('languageChanged', (language) => {
  try {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, language);
  } catch {
    // Persistence is best-effort; the in-memory language still works without it.
  }
});

export default i18n;
