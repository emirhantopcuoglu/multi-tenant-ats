import 'i18next';
import type en from './en.json';

/* Type-safe t() keys, using the English dictionary as the key shape. */
declare module 'i18next' {
  interface CustomTypeOptions {
    defaultNS: 'translation';
    resources: {
      translation: typeof en;
    };
  }
}
