import { apiClient, API_V1 } from '@/lib/apiClient';
import type { Language } from '@/i18n';

/* One endpoint per identity, since they are separate accounts. Errors aren't shown; see useLanguageSync. */

export async function setCompanyUserLanguage(language: Language): Promise<void> {
  await apiClient.put(`${API_V1}/auth/me/language`, { language });
}

export async function setCandidateLanguage(language: Language): Promise<void> {
  await apiClient.put(`${API_V1}/candidate/profile/language`, { language });
}
