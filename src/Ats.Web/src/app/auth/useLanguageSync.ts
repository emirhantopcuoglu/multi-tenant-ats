import { useEffect } from 'react';
import i18n from '@/i18n';
import type { Language } from '@/i18n';
import {
  setCandidateLanguage,
  setCompanyUserLanguage,
} from '@/features/settings/preferredLanguageApi';
import type { CurrentUser } from '@/types/auth';

/* Saves the language on the server, because emails are written later by background consumers.
   Fire-and-forget: the UI has already switched, so a failure isn't shown. Registration sends the
   language with the register request instead. */
export function useLanguageSync(user: CurrentUser | null) {
  useEffect(() => {
    if (user === null) {
      return;
    }

    const persist = user.kind === 'company' ? setCompanyUserLanguage : setCandidateLanguage;

    function handleLanguageChanged(language: string) {
      persist(language as Language).catch(() => undefined);
    }

    i18n.on('languageChanged', handleLanguageChanged);
    return () => {
      i18n.off('languageChanged', handleLanguageChanged);
    };
  }, [user]);
}
