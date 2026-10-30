import { useMutation, useQueryClient } from '@tanstack/react-query';
import { withdrawCandidateApplication } from './candidateApplicationsApi';

/* Invalidates the detail, the list, and the applied job ids, so the apply button comes back. */
export function useWithdrawApplication(applicationId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => withdrawCandidateApplication(applicationId),
    // onSettled: the likely failure is a 409 from a stale cache, so refetch then too.
    onSettled: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['candidate', 'application', applicationId] }),
        queryClient.invalidateQueries({ queryKey: ['candidate', 'applications'] }),
        queryClient.invalidateQueries({ queryKey: ['candidate', 'applied-job-ids'] }),
      ]);
    },
  });
}
