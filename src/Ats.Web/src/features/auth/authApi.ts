import { apiClient, API_V1 } from '@/lib/apiClient';
import { tokenStore } from '@/lib/tokenStore';
import type {
  AcceptInvitationRequest,
  AuthResult,
  CompanyUser,
  LoginRequest,
  RegisterRequest,
} from '@/types/auth';

/* Calls that mint tokens also store them, so callers don't have to. */

const AUTH_BASE = `${API_V1}/auth`;

export async function login(request: LoginRequest): Promise<AuthResult> {
  const { data } = await apiClient.post<AuthResult>(`${AUTH_BASE}/login`, request);
  tokenStore.setTokens(data);
  return data;
}

/* Stores no tokens: login waits for the confirmation link. */
export async function register(request: RegisterRequest): Promise<void> {
  await apiClient.post(`${AUTH_BASE}/register`, request);
}

export async function confirmEmail(userId: string, token: string): Promise<void> {
  await apiClient.post(`${AUTH_BASE}/confirm-email`, { userId, token });
}

/* Always succeeds, so it can't reveal which emails are registered. */
export async function resendEmailConfirmation(email: string): Promise<void> {
  await apiClient.post(`${AUTH_BASE}/resend-confirmation`, { email });
}

export async function getCurrentUser(): Promise<CompanyUser> {
  const { data } = await apiClient.get<Omit<CompanyUser, 'kind'>>(`${AUTH_BASE}/me`);
  return { ...data, kind: 'company' };
}

/* Returns no tokens; the caller sends the user to login. */
export async function acceptInvitation(request: AcceptInvitationRequest): Promise<void> {
  await apiClient.post(`${API_V1}/invitations/accept`, request);
}

export async function logout(): Promise<void> {
  const refreshToken = tokenStore.getRefreshToken();
  if (refreshToken) {
    // Best-effort; local credentials are cleared either way.
    await apiClient.post(`${AUTH_BASE}/logout`, { refreshToken }).catch(() => undefined);
  }
  tokenStore.clear();
}
