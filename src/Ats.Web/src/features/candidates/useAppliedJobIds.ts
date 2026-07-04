import { useQuery } from '@tanstack/react-query';
import { listAppliedJobIds } from './candidateApplicationsApi';

/* Membership set of job ids the signed-in candidate has an Active application for. Callers gate it
   with `enabled` (only a candidate token can hit the endpoint); the Set makes the per-job check on
   the public pages O(1). A fetch error is treated as "unknown" by consumers — they fall back to
   showing the apply CTA and let the backend duplicate check be the safety net. */
export function useAppliedJobIds(enabled: boolean) {
  return useQuery({
    queryKey: ['candidate', 'applied-job-ids'],
    queryFn: listAppliedJobIds,
    enabled,
    select: (ids) => new Set(ids),
  });
}
