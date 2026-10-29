import { apiClient } from '@/lib/apiClient';
import type { PagedResult } from '@/types/pagination';
import type { Job, JobDetail } from '@/types/job';

/* At the URL root, not /api/v1: the tenant comes from the slug. Sent anonymously. */

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
