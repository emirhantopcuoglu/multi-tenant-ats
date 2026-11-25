import { apiClient } from '@/lib/apiClient';
import type { PagedResult } from '@/types/pagination';
import type { InterviewStatus, InterviewType } from '@/types/enums';

export interface CandidateInterviewSummary {
  id: string;
  applicationId: string;
  jobTitle: string;
  companyName: string;
  type: InterviewType;
  scheduledAtUtc: string;
  durationMinutes: number;
  status: InterviewStatus;
  roomToken: string | null;
}

export async function listCandidateInterviews(
  page: number,
  pageSize: number,
): Promise<PagedResult<CandidateInterviewSummary>> {
  const { data } = await apiClient.get<PagedResult<CandidateInterviewSummary>>('/api/v1/candidate/interviews', {
    params: { page, pageSize },
  });
  return data;
}
