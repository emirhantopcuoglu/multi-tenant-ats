import { apiClient, API_V1 } from '@/lib/apiClient';

const CANDIDATE_AUTH_BASE = `${API_V1}/candidate/auth`;

/* Asks for a reset link. Resolves the same way whether or not the address is registered — the
   backend deliberately does not distinguish, so the UI must not either (a "no such account" message
   here would leak exactly what the endpoint refuses to). */
export async function requestCandidatePasswordReset(email: string): Promise<void> {
  await apiClient.post(`${CANDIDATE_AUTH_BASE}/forgot-password`, { email });
}

/* Sets the new password from the mailed token. Returns nothing: the reset revokes every session, so
   there is no token to store — the candidate signs in with the password they just chose. */
export async function resetCandidatePassword(request: {
  token: string;
  newPassword: string;
}): Promise<void> {
  await apiClient.post(`${CANDIDATE_AUTH_BASE}/reset-password`, request);
}
