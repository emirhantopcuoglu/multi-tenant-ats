import { apiClient, API_V1 } from '@/lib/apiClient';

const AUTH_BASE = `${API_V1}/auth`;

/* Resolves the same whether or not the address is registered. */
export async function requestPasswordReset(email: string): Promise<void> {
  await apiClient.post(`${AUTH_BASE}/forgot-password`, { email });
}

/* Returns nothing: the reset ends all sessions and the user signs in again. */
export async function resetPassword(request: {
  userId: string;
  token: string;
  newPassword: string;
}): Promise<void> {
  await apiClient.post(`${AUTH_BASE}/reset-password`, request);
}
