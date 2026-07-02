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
