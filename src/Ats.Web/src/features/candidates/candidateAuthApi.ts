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

/* The API revokes the token in the cookie and deletes the cookie. Best-effort; callers drop the
   local session either way. */
export async function candidateLogout(): Promise<void> {
  try {
    await apiClient.post(`${CANDIDATE_AUTH_BASE}/logout`);
  } catch {
    // The local session ends either way.
  }
}

export async function getCandidateMe(): Promise<CandidateUser> {
  const { data } = await apiClient.get<Omit<CandidateUser, 'kind'>>(`${CANDIDATE_AUTH_BASE}/me`);
  return { ...data, kind: 'candidate' };
}
