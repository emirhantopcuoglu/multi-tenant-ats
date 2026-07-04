import { apiClient } from '@/lib/apiClient';
import type { PagedResult } from '@/types/pagination';
import type { ApplicationStatus } from '@/types/enums';

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
