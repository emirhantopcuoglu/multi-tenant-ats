import type { AuthResult } from '@/types/auth';

/* Minimal token storage the API client needs to attach and refresh credentials. The full
   AuthContext (Step 2.1) builds on top of this; kept deliberately small for now (YAGNI).

   Storage split and its trade-off:
   - accessToken lives in memory only — it dies on reload (safer; never touches disk) and the
     interceptor silently re-mints it from the refresh token.
   - refreshToken lives in localStorage so a session survives a reload. The backend issues no
     httpOnly cookie yet, so this is the pragmatic choice; it is readable by JS and thus exposed
     to XSS. Documented as a known trade-off to revisit (cookie-based) in a later sprint. */

const REFRESH_TOKEN_KEY = 'ats-refresh-token';

let accessToken: string | null = null;

function getRefreshToken(): string | null {
  try {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  } catch {
    return null;
  }
}

export const tokenStore = {
  getAccessToken: (): string | null => accessToken,

  getRefreshToken,

  /** Persist a fresh token pair after login/register/refresh. */
  setTokens: ({ accessToken: access, refreshToken: refresh }: AuthResult): void => {
    accessToken = access;
    try {
      localStorage.setItem(REFRESH_TOKEN_KEY, refresh);
    } catch {
      // Persistence is best-effort; the in-memory access token still works for this session.
    }
  },

  /** Drop all credentials (logout, or an unrecoverable refresh failure). */
  clear: (): void => {
    accessToken = null;
    try {
      localStorage.removeItem(REFRESH_TOKEN_KEY);
    } catch {
      // Nothing to do if storage is unavailable.
    }
  },
};
