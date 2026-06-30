import { useQuery } from '@tanstack/react-query';
import { getDashboardStats } from './dashboardApi';

/* Dashboard headline counts. Cached briefly: the numbers are a coarse overview, so a slightly stale
   value is fine and avoids refetching on every visit to the home screen. */
export function useDashboardStats() {
  return useQuery({ queryKey: ['dashboard', 'stats'], queryFn: getDashboardStats, staleTime: 30_000 });
}
