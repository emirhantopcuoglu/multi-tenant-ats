import type { AuthResult } from '@/types/auth';

/* Company and candidate tokens never coexist; logging in as one clears the other. For both, the
   access token lives in memory and the refresh token in localStorage (an accepted XSS trade-off). */

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

  /* Company wins if both are set. Based on the stored refresh tokens, so it works on a fresh load. */
  getSessionKind: (): 'company' | 'candidate' | null => {
    if (getRefreshToken() !== null) return 'company';
    if (getCandidateRefreshToken() !== null) return 'candidate';
    return null;
  },

  setTokens: ({ accessToken: access, refreshToken: refresh }: AuthResult): void => {
    accessToken = access;
    try {
      localStorage.setItem(REFRESH_TOKEN_KEY, refresh);
    } catch {
      // Best-effort; the in-memory token still works for this session.
    }
  },

  setCandidateTokens: ({ accessToken: access, refreshToken: refresh }: AuthResult): void => {
    candidateAccessToken = access;
    try {
      localStorage.setItem(CANDIDATE_REFRESH_TOKEN_KEY, refresh);
    } catch {
      // Best-effort; the in-memory token still works for this session.
    }
  },

  clear: (): void => {
    accessToken = null;
    try {
      localStorage.removeItem(REFRESH_TOKEN_KEY);
    } catch {
      // Storage unavailable; nothing to clear.
    }
  },

  clearCandidateToken: (): void => {
    candidateAccessToken = null;
    try {
      localStorage.removeItem(CANDIDATE_REFRESH_TOKEN_KEY);
    } catch {
      // Storage unavailable; nothing to clear.
    }
  },
};
