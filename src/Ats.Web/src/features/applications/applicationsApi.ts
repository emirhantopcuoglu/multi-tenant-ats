import { apiClient, API_V1 } from '@/lib/apiClient';
import type { PagedResult } from '@/types/pagination';
import type {
  ApplicationActivity,
  ApplicationDetail,
  ApplicationListItem,
  CvDownloadUrl,
  CvParseResult,
  PipelineStage,
} from '@/types/application';
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

/* Served under the jobs route, but used by this feature. */
export async function listJobStages(jobId: string): Promise<PipelineStage[]> {
  const { data } = await apiClient.get<PipelineStage[]>(`${API_V1}/jobs/${jobId}/stages`);
  return data;
}

export async function getApplication(id: string): Promise<ApplicationDetail> {
  const { data } = await apiClient.get<ApplicationDetail>(`${APPLICATIONS_BASE}/${id}`);
  return data;
}

export async function getApplicationActivities(id: string): Promise<ApplicationActivity[]> {
  const { data } = await apiClient.get<ApplicationActivity[]>(`${APPLICATIONS_BASE}/${id}/activities`);
  return data;
}

/* 404 "application.cv_not_parsed" means parsing hasn't finished; callers show a processing state. */
export async function getCvParseResult(id: string): Promise<CvParseResult> {
  const { data } = await apiClient.get<CvParseResult>(`${APPLICATIONS_BASE}/${id}/cv-parse-result`);
  return data;
}

export async function getCvDownloadUrl(id: string): Promise<CvDownloadUrl> {
  const { data } = await apiClient.get<CvDownloadUrl>(`${APPLICATIONS_BASE}/${id}/cv-download-url`);
  return data;
}

/* Forward only: the backend rejects same-or-earlier and terminal stages with a 400. */
export async function moveApplicationStage(id: string, targetStageId: string): Promise<void> {
  await apiClient.post(`${APPLICATIONS_BASE}/${id}/move-stage`, { targetStageId });
}

/* Fixes a wrong move: any direction, needs a reason, never notifies the candidate. */
export async function correctApplicationStage(id: string, targetStageId: string, reason: string): Promise<void> {
  await apiClient.post(`${APPLICATIONS_BASE}/${id}/correct-stage`, { targetStageId, reason });
}

export async function rejectApplication(id: string, reason: string): Promise<void> {
  await apiClient.post(`${APPLICATIONS_BASE}/${id}/reject`, { reason });
}

/* The only way into the FinalHired stage. */
export async function hireApplication(id: string): Promise<void> {
  await apiClient.post(`${APPLICATIONS_BASE}/${id}/hire`);
}
