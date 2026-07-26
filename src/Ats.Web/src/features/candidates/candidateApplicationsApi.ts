import { apiClient } from '@/lib/apiClient';
import type { PagedResult } from '@/types/pagination';
import type { ApplicationStatus, InterviewStatus, InterviewType, PipelineStageType } from '@/types/enums';

export interface CandidateApplicationItem {
  id: string;
  jobTitle: string;
  companyName: string;
  companySlug: string;
  jobSlug: string;
  appliedAtUtc: string;
  status: ApplicationStatus;
  currentStageName: string | null;
}

/* GET /api/v1/candidate/applications/{id} — the transparent tracking view. The backend already
   sanitized it: no acting user, no internal rejection reason, stage ids resolved to names. */

export interface CandidatePipelineStage {
  id: string;
  name: string;
  type: PipelineStageType;
  order: number;
}

export type CandidateTimelineEntryType =
  | 'Submitted'
  | 'Viewed'
  | 'StageChanged'
  | 'Rejected'
  | 'Hired'
  | 'Withdrawn';

export interface CandidateTimelineEntry {
  type: CandidateTimelineEntryType;
  stageName: string | null;
  occurredAtUtc: string;
}

export interface CandidateInterview {
  id: string;
  type: InterviewType;
  scheduledAtUtc: string;
  durationMinutes: number;
  status: InterviewStatus;
}

export interface CandidateApplicationDetail {
  id: string;
  jobTitle: string;
  jobSlug: string;
  companyName: string;
  companySlug: string;
  status: ApplicationStatus;
  appliedAtUtc: string;
  firstViewedAtUtc: string | null;
  currentStageId: string;
  pipelineStages: CandidatePipelineStage[];
  timeline: CandidateTimelineEntry[];
  interviews: CandidateInterview[];
}

export async function getCandidateApplication(id: string): Promise<CandidateApplicationDetail> {
  const { data } = await apiClient.get<CandidateApplicationDetail>(
    `/api/v1/candidate/applications/${encodeURIComponent(id)}`,
  );
  return data;
}

/* The candidate closes their own application. POST, not DELETE: nothing is removed — the application
   reaches a terminal status and stays in their history. 409 means it was already closed (a stale tab);
   the caller surfaces that rather than treating it as a hard failure. */
export async function withdrawCandidateApplication(id: string): Promise<void> {
  await apiClient.post(`/api/v1/candidate/applications/${encodeURIComponent(id)}/withdraw`);
}

/* Job ids the candidate has an Active application for. Rejected/withdrawn applications are
   excluded on purpose: the backend duplicate rule allows re-applying after those, and the UI must
   agree with it. */
export async function listAppliedJobIds(): Promise<string[]> {
  const { data } = await apiClient.get<string[]>('/api/v1/candidate/applications/job-ids');
  return data;
}

export async function listCandidateApplications(
  page = 1,
  pageSize = 20,
): Promise<PagedResult<CandidateApplicationItem>> {
  const { data } = await apiClient.get<PagedResult<CandidateApplicationItem>>(
    '/api/v1/candidate/applications',
    { params: { page, pageSize } },
  );
  return data;
}
