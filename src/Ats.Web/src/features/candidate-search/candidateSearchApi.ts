import { apiClient } from '@/lib/apiClient';
import type { PagedResult } from '@/types/pagination';

/* GET /api/v1/candidates?q= — full-text search over the tenant's candidate pool, backed by a stored
   tsvector column and a GIN index. The vector covers first name, last name and email only, so the
   UI must not offer to search phone numbers or CV contents. */
export interface CandidateSearchResult {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  phone: string | null;
  linkedInUrl: string | null;
}

/* `q` is required server-side (SearchCandidatesValidator rejects an empty string), so callers must
   not fire this with a blank term — the hook gates on that rather than sending a request that is
   guaranteed to 400. */
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
