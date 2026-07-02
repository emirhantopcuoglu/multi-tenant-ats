import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { listApplications, listJobStages, type ListApplicationsParams } from './applicationsApi';
import { listJobs } from '@/features/jobs/jobsApi';

export const applicationsListKey = (params: ListApplicationsParams) =>
  ['applications', 'list', params] as const;

export function useApplications(params: ListApplicationsParams) {
  return useQuery({
    queryKey: applicationsListKey(params),
    queryFn: () => listApplications(params),
    // Keep the current page visible while the next loads (no empty flash on paging/filtering).
    placeholderData: keepPreviousData,
  });
}

/* Stages of the selected job, for the stage filter. Disabled until a job is chosen — stages are
   per-job, so "stage" only has meaning within one pipeline. */
export function useJobStages(jobId: string | undefined) {
  return useQuery({
    queryKey: ['jobs', 'stages', jobId ?? 'none'],
    queryFn: () => listJobStages(jobId as string),
    enabled: Boolean(jobId),
  });
}

// 100 is the backend's max page size; enough to populate the job filter for an MVP tenant. The list
// also resolves each row's job title from this set (the application list DTO carries only the job id).
const JOB_OPTIONS_PAGE_SIZE = 100;

export function useJobOptions() {
  return useQuery({
    queryKey: ['jobs', 'options'],
    queryFn: () => listJobs({ page: 1, pageSize: JOB_OPTIONS_PAGE_SIZE }),
    staleTime: 60_000,
  });
}
