import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { searchCandidates } from './candidateSearchApi';

/* Search is only issued for a non-blank term: the API requires `q`, so an empty box is "nothing asked
   yet", not "no results". The page reads `isPending` together with this gate to tell those apart.

   keepPreviousData holds the current rows while a new page loads, so paging doesn't flash an empty
   table — the same reason the applications list does it. */
export function useCandidateSearch(q: string, page: number, pageSize: number) {
  const term = q.trim();

  return useQuery({
    queryKey: ['candidates', 'search', term, page, pageSize],
    queryFn: () => searchCandidates(term, page, pageSize),
    enabled: term.length > 0,
    placeholderData: keepPreviousData,
  });
}
