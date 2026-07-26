import type { AuthResult } from '@/types/auth';

/* Token storage for two distinct identity types: company workspace users and candidate accounts.
   They never coexist — logging in as one clears the other (enforced in AuthProvider).

   Storage strategy — identical for both identities:
   - accessToken: memory only (re-minted from the refresh token on reload via the interceptor).
   - refreshToken: localStorage (survives reload; XSS risk, documented as known trade-off).

   The candidate access token used to live in localStorage because no refresh token existed, which
   capped a candidate session at the access token's fifteen minutes. Now that candidates rotate
   refresh tokens like company users, the short-lived half stays in memory for both. */

const REFRESH_TOKEN_KEY = 'ats-refresh-token';
const CANDIDATE_REFRESH_TOKEN_KEY = 'ats-candidate-refresh-token';

let accessToken: string | null = null;
let candidateAccessToken: string | null = null;

function getRefreshToken(): string | null {
  try {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  } catch {
    return null;
  }
}

function getCandidateRefreshToken(): string | null {
  try {
    return localStorage.getItem(CANDIDATE_REFRESH_TOKEN_KEY);
  } catch {
    return null;
  }
}

export const tokenStore = {
  getAccessToken: (): string | null => accessToken,

  getRefreshToken,

  getCandidateToken: (): string | null => candidateAccessToken,

  getCandidateRefreshToken,

  /** Derives which session type is active, used by the API interceptor to decide which refresh
   *  endpoint to call and which logout event to fire. Company takes precedence if both are set.
   *  Keyed on the persisted refresh tokens, not the in-memory access tokens, so it still answers
   *  correctly on a fresh page load before either access token has been re-minted. */
  getSessionKind: (): 'company' | 'candidate' | null => {
    if (getRefreshToken() !== null) return 'company';
    if (getCandidateRefreshToken() !== null) return 'candidate';
    return null;
  },

  /** Persist a fresh company token pair after login/register/refresh. */
  setTokens: ({ accessToken: access, refreshToken: refresh }: AuthResult): void => {
    accessToken = access;
    try {
      localStorage.setItem(REFRESH_TOKEN_KEY, refresh);
    } catch {
      // Persistence is best-effort; the in-memory access token still works for this session.
    }
  },

  /** Persist a fresh candidate token pair after login/register/refresh/password change. */
  setCandidateTokens: ({ accessToken: access, refreshToken: refresh }: AuthResult): void => {
    candidateAccessToken = access;
    try {
      localStorage.setItem(CANDIDATE_REFRESH_TOKEN_KEY, refresh);
    } catch {
      // Persistence is best-effort; the in-memory access token still works for this session.
    }
  },

  /** Drop all company credentials (logout, or an unrecoverable refresh failure). */
  clear: (): void => {
    accessToken = null;
    try {
      localStorage.removeItem(REFRESH_TOKEN_KEY);
    } catch {
      // Nothing to do if storage is unavailable.
    }
  },

  /** Drop all candidate credentials (candidate logout, or an unrecoverable refresh failure). */
  clearCandidateToken: (): void => {
    candidateAccessToken = null;
    try {
      localStorage.removeItem(CANDIDATE_REFRESH_TOKEN_KEY);
    } catch {
      // Nothing to do if storage is unavailable.
    }
  },
};
