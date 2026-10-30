import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { getMarketplaceTotals, listMarketplaceJobs, type MarketplaceJobFilters } from './marketplaceApi';

const PAGE_SIZE = 20;
const TOTALS_STALE_MS = 60_000;

export const marketplaceJobsKey = (page: number, filters: MarketplaceJobFilters) =>
  [
    'marketplace',
    'jobs',
    page,
    filters.search ?? '',
    filters.employmentType ?? '',
    filters.experienceLevel ?? '',
    filters.workArrangement ?? '',
    filters.location ?? '',
  ] as const;

export function useMarketplaceJobs(page: number, filters: MarketplaceJobFilters) {
  return useQuery({
    queryKey: marketplaceJobsKey(page, filters),
    queryFn: () => listMarketplaceJobs(page, PAGE_SIZE, filters),
    placeholderData: keepPreviousData,
  });
}

/* Not keyed by the filters: these are marketplace totals. */
export function useMarketplaceTotals() {
  return useQuery({
    queryKey: ['marketplace', 'totals'],
    queryFn: getMarketplaceTotals,
    staleTime: TOTALS_STALE_MS,
  });
}
