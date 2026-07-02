import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  archiveJob,
  closeJob,
  createJob,
  getJob,
  listJobs,
  publishJob,
  updateJob,
  type ListJobsParams,
} from './jobsApi';
import type { JobWriteRequest } from '@/types/job';

/* Root key for every jobs query, so a single invalidate after a mutation refreshes all pages/filters.
   The list key includes the params, making each filter+page combination its own cache entry. */
const JOBS_KEY = ['jobs'] as const;
export const jobsListKey = (params: ListJobsParams) => [...JOBS_KEY, 'list', params] as const;
export const jobDetailKey = (id: string) => [...JOBS_KEY, 'detail', id] as const;

export function useJobs(params: ListJobsParams) {
  return useQuery({
    queryKey: jobsListKey(params),
    queryFn: () => listJobs(params),
    // Keep the previous page on screen while the next loads, so paging/filtering doesn't flash empty.
    placeholderData: keepPreviousData,
  });
}

/* Single-job detail for the edit form. Disabled when there's no id (the create route), so the same
   hook serves both modes without a conditional call. */
export function useJob(id: string | undefined) {
  return useQuery({
    queryKey: jobDetailKey(id ?? 'new'),
    queryFn: () => getJob(id as string),
    enabled: Boolean(id),
  });
}

/* Create / update mutations for the job form. Both invalidate the jobs cache on success so the list
   and the edited detail reflect the change. */
export function useJobForm() {
  const queryClient = useQueryClient();
  const invalidate = () => queryClient.invalidateQueries({ queryKey: JOBS_KEY });

  const create = useMutation({ mutationFn: createJob, onSuccess: invalidate });
  const update = useMutation({
    mutationFn: ({ id, body }: { id: string; body: JobWriteRequest }) => updateJob(id, body),
    onSuccess: invalidate,
  });

  return { create, update };
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
