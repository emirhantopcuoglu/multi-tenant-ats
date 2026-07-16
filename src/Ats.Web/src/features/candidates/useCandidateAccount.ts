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

/* Freeze/reactivate write the new status straight into the cached "me" entry instead of
   invalidating: the status is what RequireActiveCandidate routes on, so it must flip in the same
   render pass as the mutation's onSuccess — a refetch would leave a window where the guard still
   sees the old state. */
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

/* Deletion ends the session by design (the backend rotates the security stamp), so this reuses the
   forced-logout event the API layer fires on dead tokens: AuthProvider hears it, clears the
   candidate session state and routes to the login page. */
export function useDeleteCandidateAccount() {
  return useMutation({
    mutationFn: deleteCandidateAccount,
    onSuccess: () => {
      tokenStore.clearCandidateToken();
      window.dispatchEvent(new Event(AUTH_CANDIDATE_LOGOUT_EVENT));
    },
  });
}
