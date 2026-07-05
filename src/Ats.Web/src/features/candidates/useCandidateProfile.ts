import { useMutation, useQueryClient } from '@tanstack/react-query';
import { updateCandidateProfile } from './candidateProfileApi';
import { candidateUserQueryKey } from './useCandidateCurrentUser';
import type { CandidateUser } from '@/types/auth';

/* The PUT echoes the saved profile back; write it straight into the candidate "me" cache (adding
   back the client-side `kind` discriminant) instead of invalidating, so the header's displayed
   name updates without a second round-trip. */
export function useUpdateCandidateProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: updateCandidateProfile,
    onSuccess: (profile) => {
      const user: CandidateUser = { ...profile, kind: 'candidate' };
      queryClient.setQueryData(candidateUserQueryKey, user);
    },
  });
}
