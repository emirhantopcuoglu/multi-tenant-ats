import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  cancelCandidateAccountDeletion,
  deleteCandidateAccount,
  freezeCandidateAccount,
  reactivateCandidateAccount,
} from './candidateAccountApi';
import { candidateUserQueryKey } from './useCandidateCurrentUser';
import { AUTH_CANDIDATE_LOGOUT_EVENT } from '@/lib/apiClient';
import { tokenStore } from '@/lib/tokenStore';
import type { CandidateUser } from '@/types/auth';

/* Writes the status straight into the cached "me" entry, because RequireActiveCandidate routes on it
   and must see the change immediately. None of the statuses set here has a deletion date. */
function useSetCandidateStatus() {
  const queryClient = useQueryClient();
  return (status: Exclude<CandidateUser['status'], 'PendingDeletion'>) => {
    queryClient.setQueryData<CandidateUser>(candidateUserQueryKey, (user) =>
      user ? { ...user, status, deletionDueAtUtc: null } : user,
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

export function useCancelCandidateAccountDeletion() {
  const setStatus = useSetCandidateStatus();
  return useMutation({
    mutationFn: cancelCandidateAccountDeletion,
    onSuccess: () => setStatus('Active'),
  });
}

/* The deletion request ends the session, so this reuses the forced-logout event that AuthProvider listens for. */
export function useDeleteCandidateAccount() {
  return useMutation({
    mutationFn: deleteCandidateAccount,
    onSuccess: () => {
      tokenStore.clearCandidateToken();
      window.dispatchEvent(new Event(AUTH_CANDIDATE_LOGOUT_EVENT));
    },
  });
}
