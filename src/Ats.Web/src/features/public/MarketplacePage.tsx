import { useRef } from 'react';
import { Link, Navigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Badge, Card, EmptyState, Pagination, Select, Skeleton } from '@/components/ui';
import { useAuth } from '@/app/auth/auth-context';
import { EMPLOYMENT_TYPES, EXPERIENCE_LEVELS, WORK_ARRANGEMENTS } from '@/types/enums';
import { PublicLayout } from './components/PublicLayout';
import { useMarketplaceJobs, useMarketplaceTotals } from './useMarketplaceJobs';

/* Search, filters and page live in the URL so results can be shared. */

/* Unknown enum values from the URL become "no filter". */
function sanitizeChoice(raw: string | null, allowed: readonly string[]): string {
  return raw !== null && allowed.includes(raw) ? raw : '';
}

export function MarketplacePage() {
  const { t, i18n } = useTranslation();
  const { user } = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();

  const search = searchParams.get('q') ?? '';
  const employmentType = sanitizeChoice(searchParams.get('type'), EMPLOYMENT_TYPES);
  const experienceLevel = sanitizeChoice(searchParams.get('level'), EXPERIENCE_LEVELS);
  const workArrangement = sanitizeChoice(searchParams.get('arrangement'), WORK_ARRANGEMENTS);
  const location = searchParams.get('loc') ?? '';
  const page = Math.max(1, Number(searchParams.get('page') ?? '1'));
  const hasFilters = Boolean(employmentType || experienceLevel || workArrangement || location);

  const searchInputRef = useRef<HTMLInputElement>(null);
  const locationInputRef = useRef<HTMLInputElement>(null);
  const jobsQuery = useMarketplaceJobs(page, {
    search,
    employmentType,
    experienceLevel,
    workArrangement,
    location,
  });
  const totalsQuery = useMarketplaceTotals();

  // After all hooks, so the hook order doesn't change.
  if (user?.kind === 'company') {
    return <Navigate to="/dashboard" replace />;
  }
  const jobs = jobsQuery.data?.items ?? [];
  const totalCount = jobsQuery.data?.totalCount ?? 0;
  const totalPages = jobsQuery.data?.totalPages ?? 1;
  const totals = totalsQuery.data;
  const dateFormat = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' });

  /* Every filter change goes back to page 1. */
  function updateParams(mutate: (next: URLSearchParams) => void) {
    setSearchParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        mutate(next);
        next.delete('page');
        return next;
      },
      { replace: true },
    );
  }

  function setOrDelete(next: URLSearchParams, key: string, value: string) {
    if (value) next.set(key, value);
    else next.delete(key);
  }

  function handleSearch(e: React.FormEvent) {
    e.preventDefault();
    const term = searchInputRef.current?.value.trim() ?? '';
    updateParams((next) => setOrDelete(next, 'q', term));
  }

  function handleLocationSubmit(e: React.FormEvent) {
    e.preventDefault();
    const term = locationInputRef.current?.value.trim() ?? '';
    updateParams((next) => setOrDelete(next, 'loc', term));
  }

  function handleClearFilters() {
    if (locationInputRef.current) locationInputRef.current.value = '';
    updateParams((next) => {
      next.delete('type');
      next.delete('level');
      next.delete('arrangement');
      next.delete('loc');
    });
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
    <PublicLayout wide>
      <div className="space-y-12">
        <div className="space-y-6 pt-4 text-center">
          <div className="mx-auto max-w-2xl space-y-3">
            <h1 className="text-4xl font-bold tracking-tight">{t('public.marketplace.title')}</h1>
            <p className="text-lg text-text-muted">{t('public.marketplace.subtitle')}</p>
          </div>

          <form onSubmit={handleSearch} className="mx-auto flex max-w-xl gap-2">
            <input
              ref={searchInputRef}
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

          {/* Totals ignore the active filters. Hidden until loaded so "0 open positions" never flashes. */}
          {totals && (
            <div className="flex items-center justify-center gap-6 text-sm text-text-muted">
              <span>
                <strong className="font-semibold text-text">{totals.openJobs}</strong>{' '}
                {t('public.marketplace.statsJobs', { count: totals.openJobs })}
              </span>
              <span aria-hidden="true" className="h-4 w-px bg-border" />
              <span>
                <strong className="font-semibold text-text">{totals.hiringCompanies}</strong>{' '}
                {t('public.marketplace.statsCompanies', { count: totals.hiringCompanies })}
              </span>
            </div>
          )}
        </div>

        {/* Anonymous visitors only. */}
        {!user && (
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-3 rounded-xl border border-accent/40 bg-card p-5 text-center">
              <h2 className="font-semibold text-text">{t('public.marketplace.seekerCardTitle')}</h2>
              <p className="text-sm text-text-muted">{t('public.marketplace.seekerCardText')}</p>
              <div className="flex flex-wrap justify-center gap-2">
                <Link
                  to="/candidate/login"
                  className="rounded-lg bg-accent px-4 py-2 text-sm font-medium text-accent-fg hover:bg-accent-hover"
                >
                  {t('public.marketplace.seekerSignIn')}
                </Link>
                <Link
                  to="/candidate/register"
                  className="rounded-lg border border-border px-4 py-2 text-sm font-medium text-text hover:border-accent"
                >
                  {t('public.marketplace.seekerRegister')}
                </Link>
              </div>
            </div>

            <div className="space-y-3 rounded-xl border border-border bg-card p-5 text-center">
              <h2 className="font-semibold text-text">{t('public.marketplace.hireCardTitle')}</h2>
              <p className="text-sm text-text-muted">{t('public.marketplace.hireCardText')}</p>
              <div className="flex flex-wrap justify-center gap-2">
                <Link
                  to="/login"
                  className="rounded-lg bg-text px-4 py-2 text-sm font-medium text-bg hover:opacity-90"
                >
                  {t('public.marketplace.hireSignIn')}
                </Link>
                <Link
                  to="/register"
                  className="rounded-lg border border-border px-4 py-2 text-sm font-medium text-text hover:border-accent"
                >
                  {t('public.marketplace.hireRegister')}
                </Link>
              </div>
            </div>
          </div>
        )}

        <div className="space-y-4">
          <div className="flex flex-wrap items-end justify-between gap-3">
            <div>
              <h2 className="text-xl font-semibold text-text">
                {t('public.marketplace.jobsHeading')}
              </h2>
              {jobsQuery.data && (
                <p className="text-sm text-text-muted">
                  {t('public.marketplace.resultCount', { count: totalCount })}
                </p>
              )}
            </div>

            {/* Selects apply instantly; location applies on submit. */}
            <form onSubmit={handleLocationSubmit} className="flex flex-wrap items-center gap-2">
              <Select
                aria-label={t('public.marketplace.filterType')}
                value={employmentType}
                onChange={(e) => updateParams((next) => setOrDelete(next, 'type', e.target.value))}
                className="w-40"
              >
                <option value="">{t('public.marketplace.filterType')}</option>
                {EMPLOYMENT_TYPES.map((type) => (
                  <option key={type} value={type}>
                    {t(`employmentType.${type}`)}
                  </option>
                ))}
              </Select>

              <Select
                aria-label={t('public.marketplace.filterLevel')}
                value={experienceLevel}
                onChange={(e) => updateParams((next) => setOrDelete(next, 'level', e.target.value))}
                className="w-40"
              >
                <option value="">{t('public.marketplace.filterLevel')}</option>
                {EXPERIENCE_LEVELS.map((level) => (
                  <option key={level} value={level}>
                    {t(`experienceLevel.${level}`)}
                  </option>
                ))}
              </Select>

              <Select
                aria-label={t('public.marketplace.filterWorkArrangement')}
                value={workArrangement}
                onChange={(e) => updateParams((next) => setOrDelete(next, 'arrangement', e.target.value))}
                className="w-40"
              >
                <option value="">{t('public.marketplace.filterWorkArrangement')}</option>
                {WORK_ARRANGEMENTS.map((value) => (
                  <option key={value} value={value}>
                    {t(`workArrangement.${value}`)}
                  </option>
                ))}
              </Select>

              <input
                ref={locationInputRef}
                type="search"
                defaultValue={location}
                placeholder={t('public.marketplace.filterLocation')}
                aria-label={t('public.marketplace.filterLocation')}
                onBlur={handleLocationSubmit}
                className="h-9.5 w-40 rounded-lg border border-border bg-bg px-3 text-sm text-text placeholder:text-text-muted focus:border-accent focus:outline-none focus:ring-3 focus:ring-accent-subtle"
              />

              {hasFilters && (
                <button
                  type="button"
                  onClick={handleClearFilters}
                  className="text-sm text-text-muted underline-offset-2 hover:text-text hover:underline"
                >
                  {t('public.marketplace.clearFilters')}
                </button>
              )}
            </form>
          </div>

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
              title={t(
                search || hasFilters
                  ? 'public.marketplace.empty'
                  : 'public.marketplace.emptyGeneral',
              )}
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
                          <h3 className="text-base font-semibold text-text">{job.title}</h3>
                          {job.publishedAtUtc && (
                            <span className="shrink-0 text-xs text-text-muted">
                              {dateFormat.format(new Date(job.publishedAtUtc))}
                            </span>
                          )}
                        </div>
                        <p className="text-sm font-medium text-accent">
                          {t('public.marketplace.at', { company: job.companyName })}
                        </p>
                        <div className="flex flex-wrap items-center gap-2 text-sm text-text-muted">
                          <span>{job.country ? `${job.city}, ${job.country}` : job.city}</span>
                        </div>
                        <div className="flex flex-wrap gap-2">
                          <Badge tone="neutral">{t(`employmentType.${job.employmentType}`)}</Badge>
                          <Badge tone="neutral">{t(`experienceLevel.${job.experienceLevel}`)}</Badge>
                          <Badge tone="neutral">{t(`workArrangement.${job.workArrangement}`)}</Badge>
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

        {/* Anonymous visitors only, after the jobs. */}
        {!user && (
          <div className="space-y-6 rounded-2xl border border-border bg-card p-8 text-center">
            <div className="mx-auto max-w-2xl space-y-2">
              <h2 className="text-2xl font-semibold text-text">
                {t('public.marketplace.hirePitchTitle')}
              </h2>
              <p className="text-text-muted">{t('public.marketplace.hirePitchText')}</p>
            </div>

            <div className="grid gap-4 text-left sm:grid-cols-3">
              {(['hireFeaturePosting', 'hireFeaturePipeline', 'hireFeatureCv'] as const).map(
                (feature) => (
                  <div key={feature} className="space-y-1 rounded-xl border border-border p-4">
                    <h3 className="text-sm font-semibold text-text">
                      {t(`public.marketplace.${feature}Title`)}
                    </h3>
                    <p className="text-sm text-text-muted">
                      {t(`public.marketplace.${feature}Text`)}
                    </p>
                  </div>
                ),
              )}
            </div>

            <div className="flex flex-wrap justify-center gap-2">
              <Link
                to="/register"
                className="rounded-lg bg-accent px-5 py-2.5 text-sm font-medium text-accent-fg hover:bg-accent-hover"
              >
                {t('public.marketplace.hireRegister')}
              </Link>
              <Link
                to="/login"
                className="rounded-lg border border-border px-5 py-2.5 text-sm font-medium text-text hover:border-accent"
              >
                {t('public.marketplace.hireSignIn')}
              </Link>
            </div>
          </div>
        )}
      </div>
    </PublicLayout>
  );
}
