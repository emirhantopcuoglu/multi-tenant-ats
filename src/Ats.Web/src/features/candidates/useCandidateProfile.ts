import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  changeCandidatePassword,
  confirmCandidateEmailChange,
  getCandidateProfile,
  removeCandidateCv,
  requestCandidateEmailChange,
  updateCandidateProfile,
  uploadCandidateCv,
  type CandidateCv,
  type CandidateProfile,
} from './candidateProfileApi';
import { candidateUserQueryKey } from './useCandidateCurrentUser';
import { tokenStore } from '@/lib/tokenStore';
import type { CandidateUser } from '@/types/auth';

export const candidateProfileQueryKey = ['candidate', 'profile'] as const;

/* `enabled` exists for the apply page, which needs the saved CV but runs on a public route where
   the visitor may not be a candidate — firing the request there would only earn a 401. */
export function useCandidateProfile(enabled = true) {
  return useQuery({
    queryKey: candidateProfileQueryKey,
    queryFn: getCandidateProfile,
    enabled,
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
      /* Merge instead of rebuild: the "me" entry carries fields the profile payload doesn't know
         about (account status), and a rebuilt object would silently drop them. */
      queryClient.setQueryData<CandidateUser>(candidateUserQueryKey, (user) =>
        user
          ? {
              ...user,
              email: profile.email,
              firstName: profile.firstName,
              lastName: profile.lastName,
            }
          : user,
      );
    },
  });
}

/* Swapping the stored tokens in onSuccess is not optional: the backend rotated the security stamp,
   so both the access token used for this very request AND the refresh token behind it are already
   dead. Persisting the fresh pair here is what makes the change seamless instead of a forced logout
   on the next request — every other session stays revoked, which is the point of the rotation. */
export function useChangeCandidatePassword() {
  return useMutation({
    mutationFn: changeCandidatePassword,
    onSuccess: (session) => {
      tokenStore.setCandidateTokens(session);
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

/* Both CV mutations patch the cached profile rather than invalidating it. The response carries the
   whole new state of that one field, so a refetch would ask the server to repeat what it just
   said — and the apply form reads the same cache entry to decide whether it can offer "use my
   saved CV". */
export function useUploadCandidateCv() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: uploadCandidateCv,
    onSuccess: (cv) => setCachedCv(queryClient, cv),
  });
}

export function useRemoveCandidateCv() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: removeCandidateCv,
    onSuccess: () => setCachedCv(queryClient, null),
  });
}

function setCachedCv(queryClient: ReturnType<typeof useQueryClient>, cv: CandidateCv | null) {
  queryClient.setQueryData<CandidateProfile>(candidateProfileQueryKey, (profile) =>
    profile ? { ...profile, cv } : profile,
  );
}
