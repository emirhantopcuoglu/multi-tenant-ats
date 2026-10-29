import { apiClient, API_V1 } from '@/lib/apiClient';
import type { PagedResult } from '@/types/pagination';
import type { Job, JobDetail, JobWriteRequest } from '@/types/job';
import type { JobStatus } from '@/types/enums';


const JOBS_BASE = `${API_V1}/jobs`;

export interface ListJobsParams {
  page: number;
  pageSize: number;
  /* Omitted means all statuses. */
  status?: JobStatus;
  /* Matches the title only. */
  search?: string;
}

export async function listJobs(params: ListJobsParams): Promise<PagedResult<Job>> {
  // Axios drops undefined params.
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

export async function getJob(id: string): Promise<JobDetail> {
  const { data } = await apiClient.get<JobDetail>(`${JOBS_BASE}/${id}`);
  return data;
}

export async function createJob(body: JobWriteRequest): Promise<string> {
  const { data } = await apiClient.post<{ id: string }>(JOBS_BASE, body);
  return data.id;
}

export async function updateJob(id: string, body: JobWriteRequest): Promise<void> {
  await apiClient.put(`${JOBS_BASE}/${id}`, body);
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
