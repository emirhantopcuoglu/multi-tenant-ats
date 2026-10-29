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

/* One invalidate refreshes every page and filter. */
const JOBS_KEY = ['jobs'] as const;
export const jobsListKey = (params: ListJobsParams) => [...JOBS_KEY, 'list', params] as const;
export const jobDetailKey = (id: string) => [...JOBS_KEY, 'detail', id] as const;

export function useJobs(params: ListJobsParams) {
  return useQuery({
    queryKey: jobsListKey(params),
    queryFn: () => listJobs(params),
    placeholderData: keepPreviousData,
  });
}

/* Disabled without an id, so the create route can use the same hook. */
export function useJob(id: string | undefined) {
  return useQuery({
    queryKey: jobDetailKey(id ?? 'new'),
    queryFn: () => getJob(id as string),
    enabled: Boolean(id),
  });
}

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

/* Toasts are left to the caller. */
export function useJobActions() {
  const queryClient = useQueryClient();
  const invalidate = () => queryClient.invalidateQueries({ queryKey: JOBS_KEY });

  const publish = useMutation({ mutationFn: publishJob, onSuccess: invalidate });
  const close = useMutation({ mutationFn: closeJob, onSuccess: invalidate });
  const archive = useMutation({ mutationFn: archiveJob, onSuccess: invalidate });

  return { publish, close, archive };
}
