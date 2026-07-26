import { apiClient, API_V1 } from '@/lib/apiClient';

const AUTH_BASE = `${API_V1}/auth`;

/* Asks for a reset link. Resolves the same way whether or not the address is registered — the
   backend deliberately does not distinguish, so the UI must not either. */
export async function requestPasswordReset(email: string): Promise<void> {
  await apiClient.post(`${AUTH_BASE}/forgot-password`, { email });
}

/* Sets the new password from the mailed link. Returns nothing: the reset revokes the user's refresh
   tokens, so there is no session to hand back — they sign in with the password they just chose. */
export async function resetPassword(request: {
  userId: string;
  token: string;
  newPassword: string;
}): Promise<void> {
  await apiClient.post(`${AUTH_BASE}/reset-password`, request);
}
