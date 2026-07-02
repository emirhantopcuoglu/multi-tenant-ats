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
  tokenStore.setCandidateToken(data.accessToken);
  return data;
}

export async function candidateRegister(request: CandidateRegisterRequest): Promise<CandidateAuthResult> {
  const { data } = await apiClient.post<CandidateAuthResult>(`${CANDIDATE_AUTH_BASE}/register`, request);
  tokenStore.setCandidateToken(data.accessToken);
  return data;
}

export async function getCandidateMe(): Promise<CandidateUser> {
  const { data } = await apiClient.get<Omit<CandidateUser, 'kind'>>(`${CANDIDATE_AUTH_BASE}/me`);
  return { ...data, kind: 'candidate' };
}
