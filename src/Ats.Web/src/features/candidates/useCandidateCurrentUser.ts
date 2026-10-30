import { useQuery } from '@tanstack/react-query';
import { getCandidateMe } from './candidateAuthApi';
import type { CandidateUser } from '@/types/auth';

export const candidateUserQueryKey = ['candidate', 'auth', 'me'] as const;

export function useCandidateCurrentUser(options?: { enabled?: boolean }) {
  return useQuery<CandidateUser>({
    queryKey: candidateUserQueryKey,
    queryFn: getCandidateMe,
    enabled: options?.enabled ?? true,
  });
}
