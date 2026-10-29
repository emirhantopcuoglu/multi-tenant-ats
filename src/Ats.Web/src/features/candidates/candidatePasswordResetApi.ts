import { apiClient, API_V1 } from '@/lib/apiClient';

const CANDIDATE_AUTH_BASE = `${API_V1}/candidate/auth`;

/* Resolves the same whether or not the address is registered. */
export async function requestCandidatePasswordReset(email: string): Promise<void> {
  await apiClient.post(`${CANDIDATE_AUTH_BASE}/forgot-password`, { email });
}

/* Returns nothing: the reset ends all sessions and the candidate signs in again. */
export async function resetCandidatePassword(request: {
  token: string;
  newPassword: string;
}): Promise<void> {
  await apiClient.post(`${CANDIDATE_AUTH_BASE}/reset-password`, request);
}
