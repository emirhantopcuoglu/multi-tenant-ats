import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { listApplications, listJobStages, type ListApplicationsParams } from './applicationsApi';
import { listJobs } from '@/features/jobs/jobsApi';

export const applicationsListKey = (params: ListApplicationsParams) =>
  ['applications', 'list', params] as const;

export function useApplications(params: ListApplicationsParams) {
  return useQuery({
    queryKey: applicationsListKey(params),
    queryFn: () => listApplications(params),
    placeholderData: keepPreviousData,
  });
}

/* Disabled until a job is chosen; stages are per job. */
export function useJobStages(jobId: string | undefined) {
  return useQuery({
    queryKey: ['jobs', 'stages', jobId ?? 'none'],
    queryFn: () => listJobStages(jobId as string),
    enabled: Boolean(jobId),
  });
}

// 100 is the backend's max page size. The table also uses this list for job titles.
const JOB_OPTIONS_PAGE_SIZE = 100;

export function useJobOptions() {
  return useQuery({
    queryKey: ['jobs', 'options'],
    queryFn: () => listJobs({ page: 1, pageSize: JOB_OPTIONS_PAGE_SIZE }),
    staleTime: 60_000,
  });
}
