import { useQuery } from '@tanstack/react-query';
import { getCurrentUser } from './authApi';
import type { CurrentUser } from '@/types/auth';

/* Stable cache key for the current user. Exported so mutations (login/logout) can invalidate it. */
export const currentUserQueryKey = ['auth', 'me'] as const;

/* Loads the authenticated user's profile (/auth/me) for the topbar and role-based UI.
   `enabled` lets callers defer the fetch until a token exists, so we don't fire a guaranteed 401
   on the public/login screens. */
export function useCurrentUser(options?: { enabled?: boolean }) {
  return useQuery<CurrentUser>({
    queryKey: currentUserQueryKey,
    queryFn: getCurrentUser,
    enabled: options?.enabled ?? true,
  });
}
