import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Card, EmptyState, Input, Pagination } from '@/components/ui';
import { useCandidateSearch } from './useCandidateSearch';
import type { CandidateSearchResult } from './candidateSearchApi';
import { CandidateSearchTable, CandidateSearchTableSkeleton } from './components/CandidateSearchTable';

const PAGE_SIZE = 20;
const SEARCH_DEBOUNCE_MS = 350;

function SearchIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <circle cx="11" cy="11" r="8" />
      <path d="m21 21-4.3-4.3" />
    </svg>
  );
}

/* The recruiter candidate search at /candidates, over the whole tenant's candidate pool rather than
   one job's applicants. The backend (Sprint 6.4) has been complete for a while — a stored tsvector
   column with a GIN index, websearch_to_tsquery, rank ordering — behind a placeholder screen.

   Term and page live in the URL, matching ApplicationsPage, so a search is shareable and the back
   button steps through it. */
export function CandidateSearchPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();

  const term = searchParams.get('q') ?? '';
  const pageParam = Number(searchParams.get('page'));
  const page = Number.isInteger(pageParam) && pageParam > 0 ? pageParam : 1;

  const updateParams = (mutate: (params: URLSearchParams) => void) => {
    setSearchParams(
      (previous) => {
        const next = new URLSearchParams(previous);
        mutate(next);
        return next;
      },
      { replace: true },
    );
  };

  // The input is local and the URL trails it by the debounce, so typing does not spend a request per
  // keystroke. Page is dropped on a new term: page 3 of the previous search means nothing for this one.
  const [searchInput, setSearchInput] = useState(term);
  useEffect(() => {
    const handle = setTimeout(() => {
      const trimmed = searchInput.trim();
      if (trimmed === term) return;
      updateParams((params) => {
        if (trimmed) params.set('q', trimmed);
        else params.delete('q');
        params.delete('page');
      });
    }, SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(handle);
    // Re-run when the user types or the URL settles.
  }, [searchInput, term]);

  const query = useCandidateSearch(term, page, PAGE_SIZE);
  const candidates = query.data?.items ?? [];

  /* There is no candidate detail endpoint, so a row leads to the one place that answers "who is this
     person to us": their applications. The email is the precise handle — the applications filter
     matches name or email, and two people can share a name. */
  const openApplications = (candidate: CandidateSearchResult) =>
    navigate(`/applications?q=${encodeURIComponent(candidate.email)}`);

  return (
    <div className="space-y-5">
      <div className="relative sm:max-w-md">
        <span className="pointer-events-none absolute inset-y-0 left-3 flex items-center text-text-muted">
          <SearchIcon />
        </span>
        <Input
          type="search"
          autoFocus
          aria-label={t('candidateSearch.searchLabel')}
          placeholder={t('candidateSearch.searchPlaceholder')}
          value={searchInput}
          onChange={(event) => setSearchInput(event.target.value)}
          className="pl-9"
        />
      </div>

      {/* An empty box is a distinct state from a search with no hits — the API requires a term, so
          nothing has been asked yet. Saying which fields are searchable belongs here: the tsvector
          covers name and email only, and a recruiter typing a phone number deserves to know why it
          found nothing. */}
      {term.length === 0 ? (
        <Card>
          <EmptyState
            title={t('candidateSearch.prompt.title')}
            description={t('candidateSearch.prompt.body')}
          />
        </Card>
      ) : query.isPending ? (
        <CandidateSearchTableSkeleton />
      ) : query.isError ? (
        <Card>
          <EmptyState
            title={t('candidateSearch.loadError')}
            action={
              <Button variant="secondary" onClick={() => query.refetch()}>
                {t('candidateSearch.retry')}
              </Button>
            }
          />
        </Card>
      ) : candidates.length === 0 ? (
        <Card>
          <EmptyState
            title={t('candidateSearch.noResults.title', { term })}
            description={t('candidateSearch.noResults.body')}
          />
        </Card>
      ) : (
        <div className="space-y-4">
          <CandidateSearchTable candidates={candidates} onSelect={openApplications} />
          <div className="flex flex-col items-center justify-between gap-3 sm:flex-row">
            <p className="text-sm text-text-muted">
              {t('candidateSearch.rows', { count: query.data?.totalCount ?? 0 })}
            </p>
            <Pagination
              page={page}
              pageCount={query.data?.totalPages ?? 1}
              onPageChange={(nextPage) => updateParams((params) => params.set('page', String(nextPage)))}
            />
          </div>
        </div>
      )}
    </div>
  );
}
