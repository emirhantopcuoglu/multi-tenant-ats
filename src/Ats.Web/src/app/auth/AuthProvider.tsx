import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { AUTH_CANDIDATE_LOGOUT_EVENT, AUTH_LOGOUT_EVENT } from '@/lib/apiClient';
import { tokenStore } from '@/lib/tokenStore';
import {
  login as loginRequest,
  logout as logoutRequest,
  register as registerRequest,
} from '@/features/auth/authApi';
import { currentUserQueryKey, useCurrentUser } from '@/features/auth/useCurrentUser';
import {
  candidateLogin as candidateLoginRequest,
  candidateLogout as candidateLogoutRequest,
  candidateRegister as candidateRegisterRequest,
} from '@/features/candidates/candidateAuthApi';
import {
  candidateUserQueryKey,
  useCandidateCurrentUser,
} from '@/features/candidates/useCandidateCurrentUser';
import type {
  CandidateLoginRequest,
  CandidateRegisterRequest,
  LoginRequest,
  RegisterRequest,
} from '@/types/auth';
import i18n, { type Language } from '@/i18n';
import { AuthContext, type AuthContextValue } from './auth-context';
import { useLanguageSync } from './useLanguageSync';

/* The language the interface is in right now, which is the language every email to this account
   should be written in from here on. resolvedLanguage rather than i18n.language: the latter can
   still hold a region-qualified value ("tr-TR") that the API does not accept. */
function currentLanguage(): Language {
  return i18n.resolvedLanguage as Language;
}

/* Manages authentication state for both company users and candidate accounts. At most one session
   type is active at a time — each login flow clears the other before setting itself. On cold start
   we detect which session (if any) is stored and resolve only the matching /me endpoint. */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [hasCompanySession, setHasCompanySession] = useState(
    () => tokenStore.getRefreshToken() !== null,
  );
  // Both checks read the persisted refresh token, not the in-memory access token: on a cold start
  // neither access token exists yet, and the interceptor re-mints one on the first /me call.
  const [hasCandidateSession, setHasCandidateSession] = useState(
    () => tokenStore.getCandidateRefreshToken() !== null,
  );

  const queryClient = useQueryClient();
  const navigate = useNavigate();

  const companyUserQuery = useCurrentUser({ enabled: hasCompanySession });
  const candidateUserQuery = useCandidateCurrentUser({ enabled: hasCandidateSession });

  const user = companyUserQuery.data ?? candidateUserQuery.data ?? null;

  // Mounted here because this is the one place that knows which identity is signed in; the toggle
  // itself lives in the header and has no business knowing there are two kinds of account.
  useLanguageSync(user);

  const clearCompanySession = useCallback(() => {
    setHasCompanySession(false);
    queryClient.removeQueries({ queryKey: currentUserQueryKey });
  }, [queryClient]);

  const clearCandidateSession = useCallback(() => {
    setHasCandidateSession(false);
    queryClient.removeQueries({ queryKey: candidateUserQueryKey });
  }, [queryClient]);

  const login = useCallback(
    async (credentials: LoginRequest) => {
      if (hasCandidateSession) {
        // Revoke before forgetting, the same way candidateLogin revokes the company session it
        // replaces — an abandoned refresh token must not stay redeemable for a week.
        await candidateLogoutRequest();
        tokenStore.clearCandidateToken();
        clearCandidateSession();
      }
      await loginRequest(credentials);
      setHasCompanySession(true);
      await companyUserQuery.refetch();
    },
    [hasCandidateSession, clearCandidateSession, companyUserQuery],
  );

  const register = useCallback(
    async (request: Omit<RegisterRequest, 'preferredLanguage'>) => {
      if (hasCandidateSession) {
        // Revoke before forgetting, the same way candidateLogin revokes the company session it
        // replaces — an abandoned refresh token must not stay redeemable for a week.
        await candidateLogoutRequest();
        tokenStore.clearCandidateToken();
        clearCandidateSession();
      }
      // No session is established, unlike login: registration mails a confirmation link and the caller
      // shows a "check your inbox" screen. Setting hasCompanySession here would put the app in a
      // signed-in state with no tokens, and every request would 401.
      await registerRequest({ ...request, preferredLanguage: currentLanguage() });
    },
    [hasCandidateSession, clearCandidateSession],
  );

  const candidateLogin = useCallback(
    async (credentials: CandidateLoginRequest) => {
      if (hasCompanySession) {
        await logoutRequest().catch(() => undefined);
        clearCompanySession();
      }
      await candidateLoginRequest(credentials);
      setHasCandidateSession(true);
      await candidateUserQuery.refetch();
    },
    [hasCompanySession, clearCompanySession, candidateUserQuery],
  );

  const candidateRegister = useCallback(
    async (request: Omit<CandidateRegisterRequest, 'preferredLanguage'>) => {
      if (hasCompanySession) {
        await logoutRequest().catch(() => undefined);
        clearCompanySession();
      }
      await candidateRegisterRequest({ ...request, preferredLanguage: currentLanguage() });
      setHasCandidateSession(true);
      await candidateUserQuery.refetch();
    },
    [hasCompanySession, clearCompanySession, candidateUserQuery],
  );

  const logout = useCallback(async () => {
    if (user?.kind === 'candidate') {
      // Revoke server-side first so the refresh token cannot outlive the click, then forget it
      // locally. Mirrors what logoutRequest already does for a company session.
      await candidateLogoutRequest();
      tokenStore.clearCandidateToken();
      clearCandidateSession();
      navigate('/candidate/login', { replace: true });
    } else {
      await logoutRequest();
      clearCompanySession();
      navigate('/login', { replace: true });
    }
  }, [user, clearCandidateSession, clearCompanySession, navigate]);

  useEffect(() => {
    function handleForcedLogout() {
      clearCompanySession();
      navigate('/login', { replace: true });
    }
    function handleForcedCandidateLogout() {
      clearCandidateSession();
      navigate('/candidate/login', { replace: true });
    }
    window.addEventListener(AUTH_LOGOUT_EVENT, handleForcedLogout);
    window.addEventListener(AUTH_CANDIDATE_LOGOUT_EVENT, handleForcedCandidateLogout);
    return () => {
      window.removeEventListener(AUTH_LOGOUT_EVENT, handleForcedLogout);
      window.removeEventListener(AUTH_CANDIDATE_LOGOUT_EVENT, handleForcedCandidateLogout);
    };
  }, [clearCompanySession, clearCandidateSession, navigate]);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      role: user?.kind === 'company' ? user.role : null,
      isAuthenticated: user !== null,
      isLoading:
        (hasCompanySession && companyUserQuery.isLoading) ||
        (hasCandidateSession && candidateUserQuery.isLoading),
      login,
      register,
      logout,
      candidateLogin,
      candidateRegister,
    }),
    [
      user,
      hasCompanySession,
      hasCandidateSession,
      companyUserQuery.isLoading,
      candidateUserQuery.isLoading,
      login,
      register,
      logout,
      candidateLogin,
      candidateRegister,
    ],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
