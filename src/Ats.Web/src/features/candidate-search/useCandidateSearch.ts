import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { searchCandidates } from './candidateSearchApi';

/* Only runs for a non-blank term; the page uses isPending with this to tell "nothing asked" from
   "no results". */
export function useCandidateSearch(q: string, page: number, pageSize: number) {
  const term = q.trim();

  return useQuery({
    queryKey: ['candidates', 'search', term, page, pageSize],
    queryFn: () => searchCandidates(term, page, pageSize),
    enabled: term.length > 0,
    placeholderData: keepPreviousData,
  });
}
