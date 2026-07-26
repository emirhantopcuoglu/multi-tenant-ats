import { useMutation, useQueryClient } from '@tanstack/react-query';
import { withdrawCandidateApplication } from './candidateApplicationsApi';

/* Withdrawal changes three cached things at once, so all three are invalidated rather than patched:
   the application's own detail (status + a new timeline entry), the list it appears in, and the
   applied-job-ids set the public job pages read to decide between "apply" and "already applied".
   That last one is the easy miss — leaving it stale would keep the apply button hidden on a job the
   candidate is now free to re-apply to. */
export function useWithdrawApplication(applicationId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => withdrawCandidateApplication(applicationId),
    // onSettled rather than onSuccess: the most likely failure is a 409 because the application was
    // already closed, which means the cache is the thing that is wrong. Refetching on failure too
    // lets a stale tab heal itself instead of sitting there offering an action that cannot work.
    onSettled: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['candidate', 'application', applicationId] }),
        queryClient.invalidateQueries({ queryKey: ['candidate', 'applications'] }),
        queryClient.invalidateQueries({ queryKey: ['candidate', 'applied-job-ids'] }),
      ]);
    },
  });
}
