import { apiClient, API_V1 } from '@/lib/apiClient';

/* /candidate/account — the account itself as a resource, distinct from /candidate/profile which
   edits what the account says about its owner. Freeze/reactivate are plain state flips; delete is
   permanent, so the backend demands the current password with it. */

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
  /* axios puts a DELETE body under `data` — there is no positional body argument like post(). */
  await apiClient.delete(CANDIDATE_ACCOUNT_BASE, { data: request });
}
