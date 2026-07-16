import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  changeCandidatePassword,
  confirmCandidateEmailChange,
  getCandidateProfile,
  requestCandidateEmailChange,
  updateCandidateProfile,
} from './candidateProfileApi';
import { candidateUserQueryKey } from './useCandidateCurrentUser';
import { tokenStore } from '@/lib/tokenStore';
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

/* Swapping the stored token in onSuccess is not optional: the backend rotated the security stamp,
   so the token used for this very request is already dead. Persisting the fresh one here is what
   makes the change seamless instead of a forced logout on the next request. */
export function useChangeCandidatePassword() {
  return useMutation({
    mutationFn: changeCandidatePassword,
    onSuccess: ({ accessToken }) => {
      tokenStore.setCandidateToken(accessToken);
    },
  });
}

/* No cache updates on success: the profile's email is untouched until the mailed link is used. */
export function useRequestCandidateEmailChange() {
  return useMutation({ mutationFn: requestCandidateEmailChange });
}

/* Clears the stored token on success rather than swapping it: the stamp rotation killed every
   session on purpose (email is the login identity), so the honest next step is a fresh login. */
export function useConfirmCandidateEmailChange() {
  return useMutation({
    mutationFn: confirmCandidateEmailChange,
    onSuccess: () => {
      tokenStore.clearCandidateToken();
    },
  });
}
