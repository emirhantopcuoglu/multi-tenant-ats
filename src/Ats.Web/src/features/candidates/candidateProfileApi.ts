import { apiClient, API_V1 } from '@/lib/apiClient';

/* PUT /api/v1/candidate/auth/profile. The read side reuses GET .../me (useCandidateCurrentUser) —
   the two return the same shape, so there is no separate "profile" read endpoint to call here.
   Email is read-only (login identity, no update path); only first/last name are editable. Errors
   come back as { code, message }. */

export interface CandidateProfile {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
}

export interface UpdateCandidateProfileRequest {
  firstName: string;
  lastName: string;
}

export async function updateCandidateProfile(
  request: UpdateCandidateProfileRequest,
): Promise<CandidateProfile> {
  const { data } = await apiClient.put<CandidateProfile>(
    `${API_V1}/candidate/auth/profile`,
    request,
  );
  return data;
}
