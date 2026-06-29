import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { AUTH_LOGOUT_EVENT } from '@/lib/apiClient';
import { tokenStore } from '@/lib/tokenStore';
import { login as loginRequest, logout as logoutRequest } from '@/features/auth/authApi';
import { currentUserQueryKey, useCurrentUser } from '@/features/auth/useCurrentUser';
import type { LoginRequest } from '@/types/auth';
import { AuthContext, type AuthContextValue } from './auth-context';

/* Owns authentication state for the app. On cold start it tries to resolve the current user only if
   a refresh token is present: the first /auth/me goes out without an access token (memory is empty
   on reload), 401s, and the apiClient interceptor silently refreshes from the stored token and
   retries. No refresh token (or a dead one) → the query stays disabled / errors → anonymous. */
export function AuthProvider({ children }: { children: ReactNode }) {
  // Whether we believe a session exists. Seeded from the persisted refresh token so a reload keeps
  // the user signed in without a flash of the login screen.
  const [hasSession, setHasSession] = useState(() => tokenStore.getRefreshToken() !== null);

  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const userQuery = useCurrentUser({ enabled: hasSession });
  const user = userQuery.data ?? null;

  const login = useCallback(
    async (credentials: LoginRequest) => {
      await loginRequest(credentials); // stores the token pair
      setHasSession(true);
      // Load the profile before resolving, so callers can navigate to an authenticated screen
      // knowing `user` is populated.
      await userQuery.refetch();
    },
    [userQuery],
  );

  const clearSession = useCallback(() => {
    setHasSession(false);
    queryClient.removeQueries({ queryKey: currentUserQueryKey });
  }, [queryClient]);

  const logout = useCallback(async () => {
    await logoutRequest(); // best-effort server revocation + local clear
    clearSession();
    navigate('/login', { replace: true });
  }, [clearSession, navigate]);

  // The apiClient fires this when a refresh fails unrecoverably (session truly over). React by
  // dropping to anonymous and routing to login — kept here so the API layer stays routing-agnostic.
  useEffect(() => {
    function handleForcedLogout() {
      clearSession();
      navigate('/login', { replace: true });
    }
    window.addEventListener(AUTH_LOGOUT_EVENT, handleForcedLogout);
    return () => window.removeEventListener(AUTH_LOGOUT_EVENT, handleForcedLogout);
  }, [clearSession, navigate]);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      role: user?.role ?? null,
      isAuthenticated: user !== null,
      // Only "loading" while we're actually resolving an assumed session, not when anonymous.
      isLoading: hasSession && userQuery.isLoading,
      login,
      logout,
    }),
    [user, hasSession, userQuery.isLoading, login, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
