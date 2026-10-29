import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  resendCandidateVerification,
  verifyCandidateEmail,
} from './candidateEmailVerificationApi';
import { candidateUserQueryKey } from './useCandidateCurrentUser';

export function useResendCandidateVerification() {
  return useMutation({ mutationFn: resendCandidateVerification });
}

/* Refetch "me" so the banner hides and the apply form unlocks. */
export function useVerifyCandidateEmail() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (token: string) => verifyCandidateEmail(token),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: candidateUserQueryKey }),
  });
}
