import { useQuery } from '@tanstack/react-query';
import { getCandidateMe } from './candidateAuthApi';
import type { CandidateUser } from '@/types/auth';

/* Stable cache key for the candidate profile. Exported so AuthProvider can invalidate it. */
export const candidateUserQueryKey = ['candidate', 'auth', 'me'] as const;

export function useCandidateCurrentUser(options?: { enabled?: boolean }) {
  return useQuery<CandidateUser>({
    queryKey: candidateUserQueryKey,
    queryFn: getCandidateMe,
    enabled: options?.enabled ?? true,
  });
}
