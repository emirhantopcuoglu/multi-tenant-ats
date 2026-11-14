import type { AuthResult } from '@/types/auth';

/* Company and candidate sessions never coexist; signing in as one ends the other. The access token
   lives in memory. The refresh token is an HttpOnly cookie the API sets, which no script can read;
   only the kind of session is remembered here, so a fresh page load knows which refresh to call. */

type SessionKind = 'company' | 'candidate';

const SESSION_KIND_KEY = 'ats-session-kind';
// Where refresh tokens were kept before the cookie. Still valid secrets until they expire.
const LEGACY_REFRESH_TOKEN_KEYS = ['ats-refresh-token', 'ats-candidate-refresh-token'];

let accessToken: string | null = null;
let candidateAccessToken: string | null = null;

function readSessionKind(): SessionKind | null {
  try {
    const kind = localStorage.getItem(SESSION_KIND_KEY);
    return kind === 'company' || kind === 'candidate' ? kind : null;
  } catch {
    return null;
  }
}

function writeSessionKind(kind: SessionKind | null): void {
  try {
    if (kind) localStorage.setItem(SESSION_KIND_KEY, kind);
    else localStorage.removeItem(SESSION_KIND_KEY);
  } catch {
    // Storage unavailable; the session lasts until the page is closed.
  }
}

try {
  LEGACY_REFRESH_TOKEN_KEYS.forEach((key) => localStorage.removeItem(key));
} catch {
  // Storage unavailable; nothing to remove.
}

export const tokenStore = {
  getAccessToken: (): string | null => accessToken,

  getCandidateToken: (): string | null => candidateAccessToken,

  getSessionKind: readSessionKind,

  setTokens: ({ accessToken: access }: AuthResult): void => {
    accessToken = access;
    writeSessionKind('company');
  },

  setCandidateTokens: ({ accessToken: access }: AuthResult): void => {
    candidateAccessToken = access;
    writeSessionKind('candidate');
  },

  clear: (): void => {
    accessToken = null;
    if (readSessionKind() === 'company') writeSessionKind(null);
  },

  clearCandidateToken: (): void => {
    candidateAccessToken = null;
    if (readSessionKind() === 'candidate') writeSessionKind(null);
  },
};
