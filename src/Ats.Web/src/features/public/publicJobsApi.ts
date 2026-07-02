import { apiClient } from '@/lib/apiClient';
import type { PagedResult } from '@/types/pagination';
import type { Job, JobDetail } from '@/types/job';

/* The public careers endpoints live at the URL root (e.g. /acmecorp/jobs), not under /api/v1: the
   tenant is taken from the path slug, resolved by TenantResolutionMiddleware. These calls go out
   anonymously — apiClient simply omits the Authorization header when no token is present. The slug
   is encoded so an unexpected character can't break the path. */

export async function listPublicJobs(
  slug: string,
  page = 1,
  pageSize = 20,
): Promise<PagedResult<Job>> {
  const { data } = await apiClient.get<PagedResult<Job>>(`/${encodeURIComponent(slug)}/jobs`, {
    params: { page, pageSize },
  });
  return data;
}

export async function getPublicJob(slug: string, jobSlug: string): Promise<JobDetail> {
  const { data } = await apiClient.get<JobDetail>(
    `/${encodeURIComponent(slug)}/jobs/${encodeURIComponent(jobSlug)}`,
  );
  return data;
}
