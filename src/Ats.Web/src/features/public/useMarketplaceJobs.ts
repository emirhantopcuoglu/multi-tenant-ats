import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { getMarketplaceTotals, listMarketplaceJobs, type MarketplaceJobFilters } from './marketplaceApi';

const PAGE_SIZE = 20;
const TOTALS_STALE_MS = 60_000;

/* Keyed by [page, ...filters] so each combination has its own cache entry. keepPreviousData
   prevents the list from flashing empty while the next page is loading (smoother pagination). */
export const marketplaceJobsKey = (page: number, filters: MarketplaceJobFilters) =>
  [
    'marketplace',
    'jobs',
    page,
    filters.search ?? '',
    filters.employmentType ?? '',
    filters.experienceLevel ?? '',
    filters.location ?? '',
  ] as const;

export function useMarketplaceJobs(page: number, filters: MarketplaceJobFilters) {
  return useQuery({
    queryKey: marketplaceJobsKey(page, filters),
    queryFn: () => listMarketplaceJobs(page, PAGE_SIZE, filters),
    placeholderData: keepPreviousData,
  });
}

/* Global marketplace counters for the stats strip. Deliberately NOT keyed by the active filters:
   the strip says "what this marketplace holds", not "what your search matched" — the result count
   next to the list already answers the latter. A short staleTime keeps the two bootstrap requests
   from re-firing on every filter change. */
export function useMarketplaceTotals() {
  return useQuery({
    queryKey: ['marketplace', 'totals'],
    queryFn: getMarketplaceTotals,
    staleTime: TOTALS_STALE_MS,
  });
}
