import { apiClient, API_V1 } from '@/lib/apiClient';
import { tokenStore } from '@/lib/tokenStore';
import type {
  AcceptInvitationRequest,
  AuthResult,
  CompanyUser,
  LoginRequest,
  RegisterRequest,
} from '@/types/auth';

/* Thin typed wrappers over the auth endpoints. Token persistence is a side effect of the calls
   that mint tokens, so callers (and the future AuthContext) don't have to remember to store them. */

const AUTH_BASE = `${API_V1}/auth`;

export async function login(request: LoginRequest): Promise<AuthResult> {
  const { data } = await apiClient.post<AuthResult>(`${AUTH_BASE}/login`, request);
  tokenStore.setTokens(data);
  return data;
}

export async function register(request: RegisterRequest): Promise<AuthResult> {
  const { data } = await apiClient.post<AuthResult>(`${AUTH_BASE}/register`, request);
  tokenStore.setTokens(data);
  return data;
}

export async function getCurrentUser(): Promise<CompanyUser> {
  const { data } = await apiClient.get<Omit<CompanyUser, 'kind'>>(`${AUTH_BASE}/me`);
  return { ...data, kind: 'company' };
}

/* Accept a team invitation. Returns nothing on success (the endpoint mints no tokens), so the caller
   redirects to the login screen afterwards. */
export async function acceptInvitation(request: AcceptInvitationRequest): Promise<void> {
  await apiClient.post(`${API_V1}/invitations/accept`, request);
}

export async function logout(): Promise<void> {
  const refreshToken = tokenStore.getRefreshToken();
  if (refreshToken) {
    // Best-effort server-side revocation; clear locally regardless of the result.
    await apiClient.post(`${AUTH_BASE}/logout`, { refreshToken }).catch(() => undefined);
  }
  tokenStore.clear();
}
