import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  deleteCandidateAccount,
  freezeCandidateAccount,
  reactivateCandidateAccount,
} from './candidateAccountApi';
import { candidateUserQueryKey } from './useCandidateCurrentUser';
import { AUTH_CANDIDATE_LOGOUT_EVENT } from '@/lib/apiClient';
import { tokenStore } from '@/lib/tokenStore';
import type { CandidateUser } from '@/types/auth';

/* Writes the status straight into the cached "me" entry, because RequireActiveCandidate routes on it
   and must see the change immediately. */
function useSetCandidateStatus() {
  const queryClient = useQueryClient();
  return (status: CandidateUser['status']) => {
    queryClient.setQueryData<CandidateUser>(candidateUserQueryKey, (user) =>
      user ? { ...user, status } : user,
    );
  };
}

export function useFreezeCandidateAccount() {
  const setStatus = useSetCandidateStatus();
  return useMutation({
    mutationFn: freezeCandidateAccount,
    onSuccess: () => setStatus('Frozen'),
  });
}

export function useReactivateCandidateAccount() {
  const setStatus = useSetCandidateStatus();
  return useMutation({
    mutationFn: reactivateCandidateAccount,
    onSuccess: () => setStatus('Active'),
  });
}

/* Deletion ends the session, so this reuses the forced-logout event that AuthProvider listens for. */
export function useDeleteCandidateAccount() {
  return useMutation({
    mutationFn: deleteCandidateAccount,
    onSuccess: () => {
      tokenStore.clearCandidateToken();
      window.dispatchEvent(new Event(AUTH_CANDIDATE_LOGOUT_EVENT));
    },
  });
}
