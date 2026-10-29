import { useQuery } from '@tanstack/react-query';
import { getDashboardStats } from './dashboardApi';

/* Short stale time is fine for overview numbers. */
export function useDashboardStats() {
  return useQuery({ queryKey: ['dashboard', 'stats'], queryFn: getDashboardStats, staleTime: 30_000 });
}
