import { apiClient } from '@/lib/apiClient';
import type { PagedResult } from '@/types/pagination';
import type { MarketplaceJob } from '@/types/marketplace';

/* The cross-tenant public feed at GET /public/jobs. Unlike the tenant-scoped careers pages
   (/{slug}/jobs), this endpoint has no slug and is served by PublicJobFeedController. */

export interface MarketplaceJobFilters {
  search?: string;
  employmentType?: string;
  experienceLevel?: string;
  workArrangement?: string;
  location?: string;
}

export async function listMarketplaceJobs(
  page = 1,
  pageSize = 20,
  filters: MarketplaceJobFilters = {},
): Promise<PagedResult<MarketplaceJob>> {
  const { data } = await apiClient.get<PagedResult<MarketplaceJob>>('/public/jobs', {
    params: {
      page,
      pageSize,
      search: filters.search || undefined,
      employmentType: filters.employmentType || undefined,
      experienceLevel: filters.experienceLevel || undefined,
      workArrangement: filters.workArrangement || undefined,
      location: filters.location || undefined,
    },
  });
  return data;
}

export interface MarketplaceTotals {
  openJobs: number;
  hiringCompanies: number;
}

/* The homepage stats strip. There is no dedicated stats endpoint: both list endpoints already
   return totalCount, so two pageSize=1 requests give the global numbers without any new API
   surface. If the strip ever grows beyond these two counters, that is the moment to introduce a
   real /public/stats endpoint. */
export async function getMarketplaceTotals(): Promise<MarketplaceTotals> {
  const [jobs, companies] = await Promise.all([
    apiClient.get<PagedResult<unknown>>('/public/jobs', { params: { page: 1, pageSize: 1 } }),
    apiClient.get<PagedResult<unknown>>('/public/companies', { params: { page: 1, pageSize: 1 } }),
  ]);
  return { openJobs: jobs.data.totalCount, hiringCompanies: companies.data.totalCount };
}
