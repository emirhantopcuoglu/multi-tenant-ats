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

/* A 401 here means wrong credentials, not an expired session, so it must reach the caller. */
const CREDENTIAL_ENDPOINTS = [
  `${API_V1}/auth/login`,
  `${API_V1}/auth/register`,
  `${API_V1}/candidate/auth/login`,
  `${API_V1}/candidate/auth/register`,
];

/* AuthProvider listens for this; the API layer doesn't do routing. */
export const AUTH_LOGOUT_EVENT = 'auth:logout';
export const AUTH_CANDIDATE_LOGOUT_EVENT = 'auth:candidate-logout';

const baseURL = import.meta.env.VITE_API_BASE_URL;

export const apiClient = axios.create({ baseURL });

// Company token first, then the candidate one. Anonymous calls send no header.
apiClient.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = tokenStore.getAccessToken() ?? tokenStore.getCandidateToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// So a second 401 can't loop.
interface RetriableConfig extends AxiosRequestConfig {
  _retry?: boolean;
}

/* Single-flight refresh: many requests can 401 at once, but the token may only be redeemed once,
   since a second redemption would be rejected as a replay. One slot per session kind. */
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

  // Plain axios so the interceptor can't recurse.
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

    // Not for the refresh call itself (its 401 means the token is dead) or for credential endpoints.
    if (!isAuthError || !original || original._retry || isRefreshCall || isCredentialCall) {
      return Promise.reject(error);
    }

    const sessionKind = tokenStore.getSessionKind();
    if (sessionKind === null) {
      return Promise.reject(error);
    }

    original._retry = true;

    try {
      // Kept in a local so the awaited value is a string and can't become "Bearer null".
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
      // Refresh failed: clear this identity and fire its event so AuthProvider routes to login.
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
