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

/* The apply page is public, so it only fetches for candidates. */
export function useCandidateProfile(enabled = true) {
  return useQuery({
    queryKey: candidateProfileQueryKey,
    queryFn: getCandidateProfile,
    enabled,
  });
}

/* The PUT returns the saved profile, so update both caches directly (the header name comes from "me"). */
export function useUpdateCandidateProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: updateCandidateProfile,
    onSuccess: (profile) => {
      queryClient.setQueryData(candidateProfileQueryKey, profile);
      /* Merge so fields the profile doesn't carry (status) aren't lost. */
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

/* Must store the new tokens: the stamp rotation already killed the old ones. */
export function useChangeCandidatePassword() {
  return useMutation({
    mutationFn: changeCandidatePassword,
    onSuccess: (session) => {
      tokenStore.setCandidateTokens(session);
    },
  });
}

/* Nothing to update until the link is used. */
export function useRequestCandidateEmailChange() {
  return useMutation({ mutationFn: requestCandidateEmailChange });
}

/* Every session ended, so the next step is a fresh login. */
export function useConfirmCandidateEmailChange() {
  return useMutation({
    mutationFn: confirmCandidateEmailChange,
    onSuccess: () => {
      tokenStore.clearCandidateToken();
    },
  });
}

/* Patch the cached profile with the response; the apply form reads the same entry. */
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
