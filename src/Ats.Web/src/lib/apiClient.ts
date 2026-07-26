import axios, {
  AxiosError,
  type AxiosRequestConfig,
  type InternalAxiosRequestConfig,
} from 'axios';
import { tokenStore } from './tokenStore';
import type { AuthResult } from '@/types/auth';

export const API_V1 = '/api/v1';
const REFRESH_ENDPOINT = `${API_V1}/auth/refresh`;
const CANDIDATE_REFRESH_ENDPOINT = `${API_V1}/candidate/auth/refresh`;
const HTTP_UNAUTHORIZED = 401;

/* Endpoints where the caller presents credentials. A 401 from these is a real answer (wrong email
   or password), not an expired session — the refresh flow below must let it reach the caller. */
const CREDENTIAL_ENDPOINTS = [
  `${API_V1}/auth/login`,
  `${API_V1}/auth/register`,
  `${API_V1}/candidate/auth/login`,
  `${API_V1}/candidate/auth/register`,
];

/* Fired when the refresh flow fails unrecoverably. AuthContext (Step 2.1) listens for this to
   drop the user back to /login. Emitting an event keeps the API layer free of routing concerns. */
export const AUTH_LOGOUT_EVENT = 'auth:logout';
export const AUTH_CANDIDATE_LOGOUT_EVENT = 'auth:candidate-logout';

const baseURL = import.meta.env.VITE_API_BASE_URL;

export const apiClient = axios.create({ baseURL });

// Request: attach the active access token. Company accessToken takes precedence (in-memory after
// a refresh); candidate token falls back from localStorage. Anonymous calls go out with no header.
apiClient.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = tokenStore.getAccessToken() ?? tokenStore.getCandidateToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Marker added to a request we've already retried once, so a second 401 can't loop forever.
interface RetriableConfig extends AxiosRequestConfig {
  _retry?: boolean;
}

/* Single-flight refresh: many requests can 401 at once (e.g. on first load), but we must hit the
   refresh endpoint exactly once and have the rest await that single rotation. Doubly important now
   that refresh tokens rotate on both sides — two concurrent redemptions of the same token would
   have the second one rejected as a replay, killing a session that was perfectly healthy.
   One slot per session kind, since the two identities never share a token. */
const refreshPromises: Record<'company' | 'candidate', Promise<string> | null> = {
  company: null,
  candidate: null,
};

function refreshAccessToken(kind: 'company' | 'candidate'): Promise<string> {
  const isCompany = kind === 'company';
  const refreshToken = isCompany
    ? tokenStore.getRefreshToken()
    : tokenStore.getCandidateRefreshToken();

  if (!refreshToken) {
    return Promise.reject(new Error('No refresh token available'));
  }

  const endpoint = isCompany ? REFRESH_ENDPOINT : CANDIDATE_REFRESH_ENDPOINT;

  // A bare axios call (not apiClient) so the response interceptor below can't recurse into itself.
  return axios.post<AuthResult>(`${baseURL}${endpoint}`, { refreshToken }).then((response) => {
    if (isCompany) {
      tokenStore.setTokens(response.data);
    } else {
      tokenStore.setCandidateTokens(response.data);
    }
    return response.data.accessToken;
  });
}

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config as (RetriableConfig & InternalAxiosRequestConfig) | undefined;

    const isAuthError = error.response?.status === HTTP_UNAUTHORIZED;
    const isRefreshCall =
      original?.url?.includes(REFRESH_ENDPOINT) ||
      original?.url?.includes(CANDIDATE_REFRESH_ENDPOINT);
    const isCredentialCall = CREDENTIAL_ENDPOINTS.some((endpoint) =>
      original?.url?.includes(endpoint),
    );

    // Only intervene on a genuine 401 for a request we haven't already retried. Never for the
    // refresh call itself (a 401 there means the refresh token is dead — nothing left to try),
    // and never for login/register (their 401 means invalid credentials, not a stale session).
    if (!isAuthError || !original || original._retry || isRefreshCall || isCredentialCall) {
      return Promise.reject(error);
    }

    // With no active session of either kind there is nothing to refresh — pass the error through.
    const sessionKind = tokenStore.getSessionKind();
    if (sessionKind === null) {
      return Promise.reject(error);
    }

    original._retry = true;

    try {
      // Reuse the in-flight refresh for this session kind if one is already running. Held in a local
      // so the awaited value is a plain string — re-reading the slot would type as possibly-null and
      // quietly allow a "Bearer null" header if the clearing callback ever raced the read.
      const pending =
        refreshPromises[sessionKind] ??
        refreshAccessToken(sessionKind).finally(() => {
          refreshPromises[sessionKind] = null;
        });
      refreshPromises[sessionKind] = pending;

      const newAccessToken = await pending;

      original.headers.Authorization = `Bearer ${newAccessToken}`;
      return apiClient(original);
    } catch (refreshError) {
      // Refresh failed, so this session is genuinely over. Clear that identity's credentials and
      // fire its event so AuthProvider routes to the matching login screen.
      if (sessionKind === 'company') {
        tokenStore.clear();
        window.dispatchEvent(new Event(AUTH_LOGOUT_EVENT));
      } else {
        tokenStore.clearCandidateToken();
        window.dispatchEvent(new Event(AUTH_CANDIDATE_LOGOUT_EVENT));
      }
      return Promise.reject(refreshError);
    }
  },
);
