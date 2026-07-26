import { apiClient, API_V1 } from '@/lib/apiClient';
import type { Language } from '@/i18n';

/* PUT .../language for both identities. Two endpoints rather than one because they write to two
   different tables behind two different tokens — a company user and a candidate are separate
   identities in this system, and nothing about "set my language" changes that.

   Both return 204 and neither is worth surfacing an error for: see useLanguageSync. */

export async function setCompanyUserLanguage(language: Language): Promise<void> {
  await apiClient.put(`${API_V1}/auth/me/language`, { language });
}

export async function setCandidateLanguage(language: Language): Promise<void> {
  await apiClient.put(`${API_V1}/candidate/profile/language`, { language });
}
