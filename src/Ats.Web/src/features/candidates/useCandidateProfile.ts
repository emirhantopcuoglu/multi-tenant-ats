import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getCandidateProfile, updateCandidateProfile } from './candidateProfileApi';
import { candidateUserQueryKey } from './useCandidateCurrentUser';
import type { CandidateUser } from '@/types/auth';

export const candidateProfileQueryKey = ['candidate', 'profile'] as const;

export function useCandidateProfile() {
  return useQuery({
    queryKey: candidateProfileQueryKey,
    queryFn: getCandidateProfile,
  });
}

/* The PUT echoes the saved profile back; write it straight into both caches (the profile itself
   and the auth "me" entry, which carries the name shown in the header) instead of invalidating,
   so nothing needs a second round-trip. */
export function useUpdateCandidateProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: updateCandidateProfile,
    onSuccess: (profile) => {
      queryClient.setQueryData(candidateProfileQueryKey, profile);
      const user: CandidateUser = {
        kind: 'candidate',
        id: profile.id,
        email: profile.email,
        firstName: profile.firstName,
        lastName: profile.lastName,
      };
      queryClient.setQueryData(candidateUserQueryKey, user);
    },
  });
}
