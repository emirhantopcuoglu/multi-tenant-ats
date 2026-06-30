import { apiClient, API_V1 } from '@/lib/apiClient';
import type { PagedResult } from '@/types/pagination';
import type { ApplicationListItem, PipelineStage } from '@/types/application';
import type { ApplicationStatus } from '@/types/enums';

const APPLICATIONS_BASE = `${API_V1}/applications`;

export interface ListApplicationsParams {
  page: number;
  pageSize: number;
  jobId?: string;
  stageId?: string;
  status?: ApplicationStatus;
  search?: string;
}

export async function listApplications(
  params: ListApplicationsParams,
): Promise<PagedResult<ApplicationListItem>> {
  const { data } = await apiClient.get<PagedResult<ApplicationListItem>>(APPLICATIONS_BASE, {
    params: {
      page: params.page,
      pageSize: params.pageSize,
      jobId: params.jobId,
      stageId: params.stageId,
      status: params.status,
      search: params.search?.trim() || undefined,
    },
  });
  return data;
}

/* Stages of a job's pipeline. Lives under the jobs route on the server but is an Applications concern
   on the client, so it sits in this feature next to the table that consumes it. */
export async function listJobStages(jobId: string): Promise<PipelineStage[]> {
  const { data } = await apiClient.get<PipelineStage[]>(`${API_V1}/jobs/${jobId}/stages`);
  return data;
}

/* Move an application to another stage of its job's pipeline (the Kanban drag target). Allowed only
   while the application is Active; the backend rejects terminal applications with a 400. */
export async function moveApplicationStage(id: string, targetStageId: string): Promise<void> {
  await apiClient.post(`${APPLICATIONS_BASE}/${id}/move-stage`, { targetStageId });
}
