import 'i18next';
import type en from './en.json';

/* Make t() keys type-safe: TypeScript now autocompletes keys and flags typos at compile time,
   using the English dictionary as the source of truth for the key shape. */
declare module 'i18next' {
  interface CustomTypeOptions {
    defaultNS: 'translation';
    resources: {
      translation: typeof en;
    };
  }
}
