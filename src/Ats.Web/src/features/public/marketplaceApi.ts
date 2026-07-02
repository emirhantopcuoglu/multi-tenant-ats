import { apiClient } from '@/lib/apiClient';
import type { PagedResult } from '@/types/pagination';
import type { MarketplaceJob } from '@/types/marketplace';

/* The cross-tenant public feed at GET /public/jobs. Unlike the tenant-scoped careers pages
   (/{slug}/jobs), this endpoint has no slug and is served by PublicJobFeedController. */
export async function listMarketplaceJobs(
  page = 1,
  pageSize = 20,
  search?: string,
): Promise<PagedResult<MarketplaceJob>> {
  const { data } = await apiClient.get<PagedResult<MarketplaceJob>>('/public/jobs', {
    params: { page, pageSize, search: search || undefined },
  });
  return data;
}
