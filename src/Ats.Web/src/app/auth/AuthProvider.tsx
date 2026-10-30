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

/* resolvedLanguage, not i18n.language, which may hold a region value ("tr-TR") the API rejects. */
function currentLanguage(): Language {
  return i18n.resolvedLanguage as Language;
}

/* Session state for company users and candidates. Only one is active; each login clears the other. */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [hasCompanySession, setHasCompanySession] = useState(
    () => tokenStore.getRefreshToken() !== null,
  );
  // Checks the stored refresh token; on a cold start there is no access token yet.
  const [hasCandidateSession, setHasCandidateSession] = useState(
    () => tokenStore.getCandidateRefreshToken() !== null,
  );

  const queryClient = useQueryClient();
  const navigate = useNavigate();

  const companyUserQuery = useCurrentUser({ enabled: hasCompanySession });
  const candidateUserQuery = useCandidateCurrentUser({ enabled: hasCandidateSession });

  const user = companyUserQuery.data ?? candidateUserQuery.data ?? null;

  // Mounted here because only this component knows which identity is signed in.
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
        // Revoke before forgetting, so the old refresh token can't be redeemed later.
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
        // Revoke before forgetting, so the old refresh token can't be redeemed later.
        await candidateLogoutRequest();
        tokenStore.clearCandidateToken();
        clearCandidateSession();
      }
      // No session yet: registration mails a confirmation link first.
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
      // Revoke on the server first, then forget it locally.
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
