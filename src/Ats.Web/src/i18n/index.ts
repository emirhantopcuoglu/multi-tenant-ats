import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import en from './en.json';
import tr from './tr.json';

export const SUPPORTED_LANGUAGES = ['en', 'tr'] as const;
export type Language = (typeof SUPPORTED_LANGUAGES)[number];

export const LANGUAGE_STORAGE_KEY = 'ats-lang';

const FALLBACK_LANGUAGE: Language = 'en';

function isSupported(value: string | null): value is Language {
  return value !== null && (SUPPORTED_LANGUAGES as readonly string[]).includes(value);
}

/* Saved choice first, then the browser language if supported, then English. Storage can throw. */
function getInitialLanguage(): Language {
  try {
    const saved = localStorage.getItem(LANGUAGE_STORAGE_KEY);
    if (isSupported(saved)) {
      return saved;
    }
  } catch {
    // Storage unavailable; use the browser language.
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
    // React already escapes values.
    escapeValue: false,
  },
});

i18n.on('languageChanged', (language) => {
  try {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, language);
  } catch {
    // Best-effort; the language still works without storage.
  }
});

export default i18n;
