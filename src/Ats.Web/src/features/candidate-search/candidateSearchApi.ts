import { apiClient } from '@/lib/apiClient';
import type { PagedResult } from '@/types/pagination';

/* Searches first name, last name and email only, not phone numbers or CV text. */
export interface CandidateSearchResult {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  phone: string | null;
  linkedInUrl: string | null;
}

/* `q` is required; the hook doesn't call this with a blank term. */
export async function searchCandidates(
  q: string,
  page = 1,
  pageSize = 20,
): Promise<PagedResult<CandidateSearchResult>> {
  const { data } = await apiClient.get<PagedResult<CandidateSearchResult>>('/api/v1/candidates', {
    params: { q, page, pageSize },
  });
  return data;
}
