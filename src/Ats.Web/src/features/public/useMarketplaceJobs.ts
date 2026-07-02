import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { listMarketplaceJobs } from './marketplaceApi';

const PAGE_SIZE = 20;

/* Keyed by [page, search] so each combination has its own cache entry. keepPreviousData prevents
   the list from flashing empty while the next page is loading (smoother pagination). */
export const marketplaceJobsKey = (page: number, search: string) =>
  ['marketplace', 'jobs', page, search] as const;

export function useMarketplaceJobs(page: number, search: string) {
  return useQuery({
    queryKey: marketplaceJobsKey(page, search),
    queryFn: () => listMarketplaceJobs(page, PAGE_SIZE, search || undefined),
    placeholderData: keepPreviousData,
  });
}
