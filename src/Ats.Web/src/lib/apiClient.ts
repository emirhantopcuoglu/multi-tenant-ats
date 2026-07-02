import axios, {
  AxiosError,
  type AxiosRequestConfig,
  type InternalAxiosRequestConfig,
} from 'axios';
import { tokenStore } from './tokenStore';
import type { AuthResult } from '@/types/auth';

export const API_V1 = '/api/v1';
const REFRESH_ENDPOINT = `${API_V1}/auth/refresh`;
const HTTP_UNAUTHORIZED = 401;

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

/* Single-flight refresh: many requests can 401 at once (e.g. on first load), but we must hit
   /auth/refresh exactly once and have the rest await that single rotation. While a refresh is in
   flight, `refreshPromise` is non-null and concurrent callers reuse it. */
let refreshPromise: Promise<string> | null = null;

function refreshAccessToken(): Promise<string> {
  const refreshToken = tokenStore.getRefreshToken();
  if (!refreshToken) {
    return Promise.reject(new Error('No refresh token available'));
  }

  // A bare axios call (not apiClient) so the response interceptor below can't recurse into itself.
  return axios
    .post<AuthResult>(`${baseURL}${REFRESH_ENDPOINT}`, { refreshToken })
    .then((response) => {
      tokenStore.setTokens(response.data);
      return response.data.accessToken;
    });
}

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config as (RetriableConfig & InternalAxiosRequestConfig) | undefined;

    const isAuthError = error.response?.status === HTTP_UNAUTHORIZED;
    const isRefreshCall = original?.url?.includes(REFRESH_ENDPOINT);

    // Only intervene on a genuine 401 for a request we haven't already retried, and never for the
    // refresh call itself (a 401 there means the refresh token is dead — nothing left to try).
    if (!isAuthError || !original || original._retry || isRefreshCall) {
      return Promise.reject(error);
    }

    original._retry = true;

    try {
      // Reuse the in-flight refresh if one is already running; otherwise start it.
      refreshPromise ??= refreshAccessToken().finally(() => {
        refreshPromise = null;
      });
      const newAccessToken = await refreshPromise;

      original.headers.Authorization = `Bearer ${newAccessToken}`;
      return apiClient(original);
    } catch (refreshError) {
      // Refresh failed: the session is over. Clear credentials and fire the appropriate event so
      // AuthProvider can navigate to the right login screen for each identity type.
      const kind = tokenStore.getSessionKind();
      if (kind === 'candidate') {
        tokenStore.clearCandidateToken();
        window.dispatchEvent(new Event(AUTH_CANDIDATE_LOGOUT_EVENT));
      } else {
        tokenStore.clear();
        window.dispatchEvent(new Event(AUTH_LOGOUT_EVENT));
      }
      return Promise.reject(refreshError);
    }
  },
);
