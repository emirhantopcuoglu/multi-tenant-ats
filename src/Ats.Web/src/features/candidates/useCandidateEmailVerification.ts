import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  resendCandidateVerification,
  verifyCandidateEmail,
} from './candidateEmailVerificationApi';
import { candidateUserQueryKey } from './useCandidateCurrentUser';

export function useResendCandidateVerification() {
  return useMutation({ mutationFn: resendCandidateVerification });
}

/* On success the cached "me" entry is invalidated: isEmailVerified is what hides the banner and
   unblocks the apply form, so it has to be refetched or a candidate who verifies in this tab keeps
   being told to check their inbox. */
export function useVerifyCandidateEmail() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (token: string) => verifyCandidateEmail(token),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: candidateUserQueryKey }),
  });
}
