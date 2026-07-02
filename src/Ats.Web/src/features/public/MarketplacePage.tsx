import { useRef } from 'react';
import { Link, Navigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Badge, Card, EmptyState, Pagination, Skeleton } from '@/components/ui';
import { useAuth } from '@/app/auth/auth-context';
import { PublicLayout } from './components/PublicLayout';
import { useMarketplaceJobs } from './useMarketplaceJobs';

/* Cross-tenant public job marketplace. Sits at / so it is the first page any visitor sees.
   Search and page are kept in URL query params (?q=&page=) so results are shareable and
   survive a browser refresh. */
export function MarketplacePage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();

  // Company users have their own dashboard; the marketplace is for candidates and anonymous visitors.
  if (user?.kind === 'company') {
    return <Navigate to="/dashboard" replace />;
  }

  const search = searchParams.get('q') ?? '';
  const page = Math.max(1, Number(searchParams.get('page') ?? '1'));

  const inputRef = useRef<HTMLInputElement>(null);
  const jobsQuery = useMarketplaceJobs(page, search);
  const jobs = jobsQuery.data?.items ?? [];
  const totalPages = jobsQuery.data?.totalPages ?? 1;

  function handleSearch(e: React.FormEvent) {
    e.preventDefault();
    const term = inputRef.current?.value.trim() ?? '';
    setSearchParams(term ? { q: term } : {}, { replace: true });
  }

  function handlePageChange(newPage: number) {
    setSearchParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        next.set('page', String(newPage));
        return next;
      },
      { replace: true },
    );
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  return (
    <PublicLayout>
      <div className="space-y-10">
        {/* Hero */}
        <div className="space-y-6 text-center">
          <div className="space-y-2">
            <h1 className="text-3xl font-bold tracking-tight">{t('public.marketplace.title')}</h1>
            <p className="text-text-muted">{t('public.marketplace.subtitle')}</p>
          </div>

          <form onSubmit={handleSearch} className="mx-auto flex max-w-xl gap-2">
            <input
              ref={inputRef}
              type="search"
              defaultValue={search}
              placeholder={t('public.marketplace.searchPlaceholder')}
              aria-label={t('public.marketplace.searchPlaceholder')}
              className="flex-1 rounded-lg border border-border bg-card px-4 py-2.5 text-sm text-text placeholder:text-text-muted focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/30"
            />
            <button
              type="submit"
              className="rounded-lg bg-accent px-5 py-2.5 text-sm font-medium text-accent-fg hover:bg-accent-hover focus:outline-none focus:ring-2 focus:ring-accent/40"
            >
              {t('public.marketplace.searchBtn')}
            </button>
          </form>
        </div>

        {/* Results */}
        <div className="space-y-4">
          {jobsQuery.isLoading ? (
            <div className="space-y-3" aria-busy="true" aria-label="Loading">
              <Skeleton className="h-24 w-full" />
              <Skeleton className="h-24 w-full" />
              <Skeleton className="h-24 w-full" />
              <Skeleton className="h-24 w-full" />
              <Skeleton className="h-24 w-full" />
            </div>
          ) : jobsQuery.isError ? (
            <EmptyState
              title={t('common.errorTitle')}
              description={t('common.errorRetry')}
            />
          ) : jobs.length === 0 ? (
            <EmptyState
              title={t(search ? 'public.marketplace.empty' : 'public.marketplace.emptyGeneral')}
            />
          ) : (
            <>
              <ul className="space-y-3">
                {jobs.map((job) => (
                  <li key={job.id}>
                    <Link
                      to={`/${job.companySlug}/jobs/${job.slug}`}
                      className="block"
                    >
                      <Card className="space-y-2 transition-colors hover:border-accent">
                        <div className="flex items-start justify-between gap-4">
                          <h2 className="text-base font-semibold text-text">{job.title}</h2>
                        </div>
                        <p className="text-sm font-medium text-accent">
                          {t('public.marketplace.at', { company: job.companyName })}
                        </p>
                        <div className="flex flex-wrap items-center gap-2 text-sm text-text-muted">
                          <span>{job.location}</span>
                        </div>
                        <div className="flex flex-wrap gap-2">
                          <Badge tone="neutral">{t(`employmentType.${job.employmentType}`)}</Badge>
                          <Badge tone="neutral">{t(`experienceLevel.${job.experienceLevel}`)}</Badge>
                        </div>
                      </Card>
                    </Link>
                  </li>
                ))}
              </ul>

              <div className="flex justify-center pt-2">
                <Pagination
                  page={page}
                  pageCount={totalPages}
                  onPageChange={handlePageChange}
                />
              </div>
            </>
          )}
        </div>
      </div>
    </PublicLayout>
  );
}
