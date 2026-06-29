import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { archiveJob, closeJob, listJobs, publishJob, type ListJobsParams } from './jobsApi';

/* Root key for every jobs query, so a single invalidate after a mutation refreshes all pages/filters.
   The list key includes the params, making each filter+page combination its own cache entry. */
const JOBS_KEY = ['jobs'] as const;
export const jobsListKey = (params: ListJobsParams) => [...JOBS_KEY, 'list', params] as const;

export function useJobs(params: ListJobsParams) {
  return useQuery({
    queryKey: jobsListKey(params),
    queryFn: () => listJobs(params),
    // Keep the previous page on screen while the next loads, so paging/filtering doesn't flash empty.
    placeholderData: keepPreviousData,
  });
}

/* The three lifecycle mutations. Each invalidates the whole jobs cache on success so the list (and
   any future detail view) reflects the new status. Toast feedback is left to the caller. */
export function useJobActions() {
  const queryClient = useQueryClient();
  const invalidate = () => queryClient.invalidateQueries({ queryKey: JOBS_KEY });

  const publish = useMutation({ mutationFn: publishJob, onSuccess: invalidate });
  const close = useMutation({ mutationFn: closeJob, onSuccess: invalidate });
  const archive = useMutation({ mutationFn: archiveJob, onSuccess: invalidate });

  return { publish, close, archive };
}
