import { apiClient, API_V1 } from '@/lib/apiClient';
import { tokenStore } from '@/lib/tokenStore';
import type {
  CandidateAuthResult,
  CandidateLoginRequest,
  CandidateRegisterRequest,
  CandidateUser,
} from '@/types/auth';

const CANDIDATE_AUTH_BASE = `${API_V1}/candidate/auth`;

export async function candidateLogin(request: CandidateLoginRequest): Promise<CandidateAuthResult> {
  const { data } = await apiClient.post<CandidateAuthResult>(`${CANDIDATE_AUTH_BASE}/login`, request);
  tokenStore.setCandidateTokens(data);
  return data;
}

export async function candidateRegister(request: CandidateRegisterRequest): Promise<CandidateAuthResult> {
  const { data } = await apiClient.post<CandidateAuthResult>(`${CANDIDATE_AUTH_BASE}/register`, request);
  tokenStore.setCandidateTokens(data);
  return data;
}

/* Revokes the refresh token server-side before the client forgets it. Best-effort: a failure here
   still has to end the local session, so callers drop credentials regardless of the outcome. */
export async function candidateLogout(): Promise<void> {
  const refreshToken = tokenStore.getCandidateRefreshToken();
  if (!refreshToken) return;

  try {
    await apiClient.post(`${CANDIDATE_AUTH_BASE}/logout`, { refreshToken });
  } catch {
    // The session ends locally either way; a dead token on the server is harmless.
  }
}

export async function getCandidateMe(): Promise<CandidateUser> {
  const { data } = await apiClient.get<Omit<CandidateUser, 'kind'>>(`${CANDIDATE_AUTH_BASE}/me`);
  return { ...data, kind: 'candidate' };
}
