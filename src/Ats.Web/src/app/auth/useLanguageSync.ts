import { useEffect } from 'react';
import i18n from '@/i18n';
import type { Language } from '@/i18n';
import {
  setCandidateLanguage,
  setCompanyUserLanguage,
} from '@/features/settings/preferredLanguageApi';
import type { CurrentUser } from '@/types/auth';

/* Keeps the server's copy of the language in step with the header toggle.

   The server needs it because emails are composed outside the browser: a rejection letter is
   written by a background consumer days after the candidate last had a tab open, and the only
   record of which language to write it in is the one this hook pushes.

   Deliberately fire-and-forget, the same shape as the best-effort logout in AuthProvider. Nothing
   in the UI depends on the write landing — the interface has already switched language locally — so
   a failure must not raise a toast about an action the user never knowingly took. The cost is that
   a rejected write is invisible here: the next email arrives in the previous language and the user
   switches again. The server-side log of the failed request is where that gets noticed.

   Registration is not covered here: the account does not exist yet when the language is chosen, so
   the value travels on the register request itself. */
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
