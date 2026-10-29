import { useQuery } from '@tanstack/react-query';
import { listAppliedJobIds } from './candidateApplicationsApi';

/* Enabled only for candidates. On error, callers show the apply button and let the backend catch
   duplicates. */
export function useAppliedJobIds(enabled: boolean) {
  return useQuery({
    queryKey: ['candidate', 'applied-job-ids'],
    queryFn: listAppliedJobIds,
    enabled,
    select: (ids) => new Set(ids),
  });
}
