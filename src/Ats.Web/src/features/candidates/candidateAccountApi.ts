import { apiClient, API_V1 } from '@/lib/apiClient';

/* Deleting needs the current password. */

const CANDIDATE_ACCOUNT_BASE = `${API_V1}/candidate/account`;

export async function freezeCandidateAccount(): Promise<void> {
  await apiClient.post(`${CANDIDATE_ACCOUNT_BASE}/freeze`);
}

export async function reactivateCandidateAccount(): Promise<void> {
  await apiClient.post(`${CANDIDATE_ACCOUNT_BASE}/reactivate`);
}

export interface DeleteCandidateAccountRequest {
  currentPassword: string;
}

export async function deleteCandidateAccount(
  request: DeleteCandidateAccountRequest,
): Promise<void> {
  /* axios puts a DELETE body under `data`. */
  await apiClient.delete(CANDIDATE_ACCOUNT_BASE, { data: request });
}
