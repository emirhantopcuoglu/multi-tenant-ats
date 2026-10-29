import { apiClient, API_V1 } from '@/lib/apiClient';

const CANDIDATE_AUTH_BASE = `${API_V1}/candidate/auth`;

/* Anonymous: the link may be opened on another device. */
export async function verifyCandidateEmail(token: string): Promise<void> {
  await apiClient.post(`${CANDIDATE_AUTH_BASE}/verify-email`, { token });
}

/* Sends to the signed-in account's own address. */
export async function resendCandidateVerification(): Promise<void> {
  await apiClient.post(`${CANDIDATE_AUTH_BASE}/resend-verification`);
}
