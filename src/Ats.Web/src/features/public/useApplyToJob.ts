import { useMutation, useQueryClient } from '@tanstack/react-query';
import { submitApplication, type ApplyRequest } from './applyApi';

/* Invalidates the candidate caches so "applied" shows without a reload. */
export function useApplyToJob(slug: string, jobSlug: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: ApplyRequest) => submitApplication(slug, jobSlug, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['candidate', 'applied-job-ids'] });
      void queryClient.invalidateQueries({ queryKey: ['candidate', 'applications'] });
    },
  });
}
