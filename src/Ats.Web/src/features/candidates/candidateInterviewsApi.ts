import { apiClient } from '@/lib/apiClient';
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

export async function listCandidateInterviews(): Promise<CandidateInterviewSummary[]> {
  const { data } = await apiClient.get<CandidateInterviewSummary[]>('/api/v1/candidate/interviews');
  return data;
}
