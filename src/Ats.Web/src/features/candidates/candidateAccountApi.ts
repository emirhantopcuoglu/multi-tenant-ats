import { apiClient, API_V1 } from '@/lib/apiClient';

/* Deleting needs the current password, and only schedules the erasure. */

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

export interface DeletionRequested {
  deletionDueAtUtc: string;
}

export async function deleteCandidateAccount(
  request: DeleteCandidateAccountRequest,
): Promise<DeletionRequested> {
  /* axios puts a DELETE body under `data`. */
  const { data } = await apiClient.delete<DeletionRequested>(CANDIDATE_ACCOUNT_BASE, { data: request });
  return data;
}

export async function cancelCandidateAccountDeletion(): Promise<void> {
  await apiClient.post(`${CANDIDATE_ACCOUNT_BASE}/cancel-deletion`);
}
