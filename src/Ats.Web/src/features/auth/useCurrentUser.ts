import { useQuery } from '@tanstack/react-query';
import { getCurrentUser } from './authApi';
import type { CompanyUser } from '@/types/auth';

export const currentUserQueryKey = ['auth', 'me'] as const;

/* `enabled` lets callers wait for a token, avoiding a certain 401 on public pages. */
export function useCurrentUser(options?: { enabled?: boolean }) {
  return useQuery<CompanyUser>({
    queryKey: currentUserQueryKey,
    queryFn: getCurrentUser,
    enabled: options?.enabled ?? true,
  });
}
