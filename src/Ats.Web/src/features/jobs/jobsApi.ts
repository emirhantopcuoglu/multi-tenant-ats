import { apiClient, API_V1 } from '@/lib/apiClient';
import type { PagedResult } from '@/types/pagination';
import type { Job } from '@/types/job';
import type { JobStatus } from '@/types/enums';

/* Typed wrappers over the recruiter Jobs endpoints (JobsController). Lifecycle transitions are POSTs
   that return 204 No Content, so their wrappers resolve to void. */

const JOBS_BASE = `${API_V1}/jobs`;

export interface ListJobsParams {
  page: number;
  pageSize: number;
  /** Single status filter; omitted means "all statuses". */
  status?: JobStatus;
  /** Title contains-search; the backend matches on Title only. */
  search?: string;
}

export async function listJobs(params: ListJobsParams): Promise<PagedResult<Job>> {
  // Axios drops params whose value is undefined, so empty filters simply aren't sent.
  const { data } = await apiClient.get<PagedResult<Job>>(JOBS_BASE, {
    params: {
      page: params.page,
      pageSize: params.pageSize,
      status: params.status,
      search: params.search?.trim() || undefined,
    },
  });
  return data;
}

export async function publishJob(id: string): Promise<void> {
  await apiClient.post(`${JOBS_BASE}/${id}/publish`);
}

export async function closeJob(id: string): Promise<void> {
  await apiClient.post(`${JOBS_BASE}/${id}/close`);
}

export async function archiveJob(id: string): Promise<void> {
  await apiClient.post(`${JOBS_BASE}/${id}/archive`);
}
