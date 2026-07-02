import type { AuthResult } from '@/types/auth';

/* Token storage for two distinct identity types: company workspace users and candidate accounts.
   They never coexist — logging in as one clears the other (enforced in AuthProvider).

   Storage strategy:
   - Company accessToken: memory only (re-minted from refreshToken on reload via interceptor).
   - Company refreshToken: localStorage (survives reload; XSS risk, documented as known trade-off).
   - Candidate accessToken: localStorage (no refresh token issued; persisting it in localStorage
     lets the session survive a reload at the same XSS risk level as the company refresh token). */

const REFRESH_TOKEN_KEY = 'ats-refresh-token';
const CANDIDATE_TOKEN_KEY = 'ats-candidate-token';

let accessToken: string | null = null;

function getRefreshToken(): string | null {
  try {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  } catch {
    return null;
  }
}

function getCandidateToken(): string | null {
  try {
    return localStorage.getItem(CANDIDATE_TOKEN_KEY);
  } catch {
    return null;
  }
}

export const tokenStore = {
  getAccessToken: (): string | null => accessToken,

  getRefreshToken,

  getCandidateToken,

  /** Derives which session type is active, used by the API interceptor to fire the right logout
   *  event when an unrecoverable 401 occurs. Company takes precedence if both keys are somehow set. */
  getSessionKind: (): 'company' | 'candidate' | null => {
    if (getRefreshToken() !== null) return 'company';
    if (getCandidateToken() !== null) return 'candidate';
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

  /** Persist a candidate access token after login/register. */
  setCandidateToken: (token: string): void => {
    try {
      localStorage.setItem(CANDIDATE_TOKEN_KEY, token);
    } catch {
      // Best-effort; the UI will force re-login if the token can't be read back.
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

  /** Drop the candidate access token (candidate logout or unrecoverable 401). */
  clearCandidateToken: (): void => {
    try {
      localStorage.removeItem(CANDIDATE_TOKEN_KEY);
    } catch {
      // Nothing to do if storage is unavailable.
    }
  },
};
