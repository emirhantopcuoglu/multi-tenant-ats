import { apiClient, API_V1 } from '@/lib/apiClient';

const CANDIDATE_AUTH_BASE = `${API_V1}/candidate/auth`;

/* Consumes the token from a mailed verification link. Anonymous on the server: the link is clicked
   from an email client, possibly on a different device than the one that registered. */
export async function verifyCandidateEmail(token: string): Promise<void> {
  await apiClient.post(`${CANDIDATE_AUTH_BASE}/verify-email`, { token });
}

/* Re-sends the link to the address already on the signed-in account. Authenticated, so there is no
   way to aim it at a stranger's mailbox and no need for the anti-enumeration silence that
   forgot-password requires. */
export async function resendCandidateVerification(): Promise<void> {
  await apiClient.post(`${CANDIDATE_AUTH_BASE}/resend-verification`);
}
